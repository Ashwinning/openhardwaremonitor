﻿/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2012 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Permissions;
using System.Reflection;

namespace OpenHardwareMonitor.Hardware {

  public class Computer : IComputer {

    private readonly List<IGroup> groups = new List<IGroup>();
    private readonly ISettings settings;

    private SMBIOS smbios;

    private bool open;

    private bool mainboardEnabled;
    private bool cpuEnabled;
    private bool ramEnabled;
    private bool gpuEnabled;
    private bool fanControllerEnabled;
    private bool hddEnabled;    

    public Computer() {
      this.settings = new Settings();
    }

    public Computer(ISettings settings) {
      this.settings = settings ?? new Settings();
    }

    private void Add(IGroup group) {
      if (groups.Contains(group))
        return;

      groups.Add(group);

      if (HardwareAdded != null)
        foreach (IHardware hardware in group.Hardware)
          HardwareAdded(hardware);
    }

    private void Remove(IGroup group) {
      if (!groups.Contains(group))
        return;

      groups.Remove(group);

      if (HardwareRemoved != null)
        foreach (IHardware hardware in group.Hardware)
          HardwareRemoved(hardware);

      group.Close();
    }

    private void RemoveType<T>() where T : IGroup {
      List<IGroup> list = new List<IGroup>();
      foreach (IGroup group in groups)
        if (group is T)
          list.Add(group);
      foreach (IGroup group in list)
        Remove(group);
    }

    [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
    public void Open() {
      if (open)
        return;

      this.smbios = new SMBIOS();

      Ring0.Open();
      Opcode.Open();

      if (mainboardEnabled)
        Add(new Mainboard.MainboardGroup(smbios, settings));
      
      if (cpuEnabled)
        Add(new CPU.CPUGroup(settings));

      if (ramEnabled)
        Add(new RAM.RAMGroup(smbios, settings));

      if (gpuEnabled) {
        Add(new ATI.ATIGroup(settings));
        Add(new Nvidia.NvidiaGroup(settings));
      }

      if (fanControllerEnabled) {
        Add(new TBalancer.TBalancerGroup(settings));
        Add(new Heatmaster.HeatmasterGroup(settings));
      }

      if (hddEnabled)
        Add(new HDD.HarddriveGroup(settings));

      open = true;
    }

    public bool MainboardEnabled {
      get { return mainboardEnabled; }

      [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
      set {
        if (open && value != mainboardEnabled) {
          if (value)
            Add(new Mainboard.MainboardGroup(smbios, settings));
          else
            RemoveType<Mainboard.MainboardGroup>();
        }
        mainboardEnabled = value;
      }
    }

    public bool CPUEnabled {
      get { return cpuEnabled; }

      [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
      set {
        if (open && value != cpuEnabled) {
          if (value)
            Add(new CPU.CPUGroup(settings));
          else
            RemoveType<CPU.CPUGroup>();
        }
        cpuEnabled = value;
      }
    }

    public bool RAMEnabled {
      get { return ramEnabled; }

      [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
      set {
        if (open && value != ramEnabled) {
          if (value)
            Add(new RAM.RAMGroup(smbios, settings));
          else
            RemoveType<RAM.RAMGroup>();
        }
        ramEnabled = value;
      }
    }    

    public bool GPUEnabled {
      get { return gpuEnabled; }

      [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
      set {
        if (open && value != gpuEnabled) {
          if (value) {
            Add(new ATI.ATIGroup(settings));
            Add(new Nvidia.NvidiaGroup(settings));
          } else {
            RemoveType<ATI.ATIGroup>();
            RemoveType<Nvidia.NvidiaGroup>();
          }
        }
        gpuEnabled = value;
      }
    }

    public bool FanControllerEnabled {
      get { return fanControllerEnabled; }

      [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
      set {
        if (open && value != fanControllerEnabled) {
          if (value) {
            Add(new TBalancer.TBalancerGroup(settings));
            Add(new Heatmaster.HeatmasterGroup(settings));
          } else {
            RemoveType<TBalancer.TBalancerGroup>();
            RemoveType<Heatmaster.HeatmasterGroup>();
          }
        }
        fanControllerEnabled = value;
      }
    }

    public bool HDDEnabled {
      get { return hddEnabled; }

      [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
      set {
        if (open && value != hddEnabled) {
          if (value)
            Add(new HDD.HarddriveGroup(settings));
          else
            RemoveType<HDD.HarddriveGroup>();
        }
        hddEnabled = value;
      }
    }

    public IHardware[] Hardware {
      get {
        List<IHardware> list = new List<IHardware>();
        foreach (IGroup group in groups)
          foreach (IHardware hardware in group.Hardware)
            list.Add(hardware);
        return list.ToArray();
      }
    }

#region PRIVATE_FUNCTIONS

    private static void NewSection(TextWriter writer) {
      for (int i = 0; i < 8; i++)
        writer.Write("----------");
      writer.WriteLine();
      writer.WriteLine();
    }

    private static int CompareSensor(ISensor a, ISensor b) {
      int c = a.SensorType.CompareTo(b.SensorType);
      if (c == 0)
        return a.Index.CompareTo(b.Index);
      else
        return c;
    }

    private static void ReportHardwareSensorTree(
      IHardware hardware, TextWriter w, string space) 
    {
      w.WriteLine("{0}|", space);
      w.WriteLine("{0}+- {1} ({2})",
        space, hardware.Name, hardware.Identifier);
      ISensor[] sensors = hardware.Sensors;
      Array.Sort(sensors, CompareSensor);
      foreach (ISensor sensor in sensors) {
        w.WriteLine("{0}|  +- {1,-14} : {2,8:G6} {3,8:G6} {4,8:G6} ({5})", 
          space, sensor.Name, sensor.Value, sensor.Min, sensor.Max, 
          sensor.Identifier);
      }
      foreach (IHardware subHardware in hardware.SubHardware)
        ReportHardwareSensorTree(subHardware, w, "|  ");
    }

    private static void ReportHardwareParameterTree(
      IHardware hardware, TextWriter w, string space) {
      w.WriteLine("{0}|", space);
      w.WriteLine("{0}+- {1} ({2})",
        space, hardware.Name, hardware.Identifier);
      ISensor[] sensors = hardware.Sensors;
      Array.Sort(sensors, CompareSensor);
      foreach (ISensor sensor in sensors) {
        string innerSpace = space + "|  ";
        if (sensor.Parameters.Length > 0) {
          w.WriteLine("{0}|", innerSpace);
          w.WriteLine("{0}+- {1} ({2})",
            innerSpace, sensor.Name, sensor.Identifier);
          foreach (IParameter parameter in sensor.Parameters) {
            string innerInnerSpace = innerSpace + "|  ";
            w.WriteLine("{0}+- {1} : {2}",
              innerInnerSpace, parameter.Name,
              string.Format(CultureInfo.InvariantCulture, "{0} : {1}",
                parameter.DefaultValue, parameter.Value));
          }
        }
      }
      foreach (IHardware subHardware in hardware.SubHardware)
        ReportHardwareParameterTree(subHardware, w, "|  ");
    }

    private static void ReportHardware(IHardware hardware, TextWriter w) {
      string hardwareReport = hardware.GetReport();
      if (!string.IsNullOrEmpty(hardwareReport)) {
        NewSection(w);
        w.Write(hardwareReport);
      }
      foreach (IHardware subHardware in hardware.SubHardware)
        ReportHardware(subHardware, w);
    }

        #endregion

        #region PERFBASE_FUNCTIONS
        /*
                /// <summary>
                /// Returns the configuration of a computer.
                /// </summary>
                /// <returns></returns>
                public string GetConfig()
                {
                    Perfbase.Config config = new Perfbase.Config();
                    config.computerName = Environment.MachineName.ToString(); //TODO
                    config.os = Environment.OSVersion.ToString();
                    config.osType = IntPtr.Size == 4 ? "32-Bit" : "64-Bit";
                    config.motherboardModel = GetNameFor(HardwareType.Mainboard);
                    config.cpuModel = GetNameFor(HardwareType.CPU);
                    config.ramSpace = 
                    config.gpu = "";
                    config.totalHDDSpace = "";
                    config.hdd = "";


                    return "";

                }
        */

        public string Test()
        {
            return "test successful";
        }

        private SensorType lastSensorType;  //hold a reference to the last sensortype we were looking at

        public List<Perfbase.HardwareStats> GetPerfReport()
        {

            List<Perfbase.HardwareStats> hwStatList = new List<Perfbase.HardwareStats>();

            foreach (IGroup group in groups)
            {
                foreach (IHardware hardware in group.Hardware)
                    ReportPerfSensorTree(hardware, hwStatList);
            }

            return hwStatList;

        }

        private void ReportPerfSensorTree(IHardware hardware, List<Perfbase.HardwareStats> hwStatList)
        {
            Perfbase.HardwareStats hwStats = new Perfbase.HardwareStats();
            hwStats.name = hardware.Name; //Store the name of the hardware
            hwStats.sensorTypes = new List<Perfbase.SensorType>();

            ISensor[] sensors = hardware.Sensors;
            Array.Sort(sensors, CompareSensor);

            lastSensorType = sensors[0].SensorType; //Initialize this with the value of the first sensor

            int sensorTypeObjectsFoundSoFar = 0; //keep track of how many sensortype objects we have
            hwStats.sensorTypes.Add(new Perfbase.SensorType()); //Initialize the list with a new object
            hwStats.sensorTypes[sensorTypeObjectsFoundSoFar].name = lastSensorType.ToString(); //Initialize the name for the first one

            foreach (ISensor sensor in sensors)
            {
                //Create a new sensor object and set it's values
                Perfbase.Sensor sensorObject = new Perfbase.Sensor();
                sensorObject.name = sensor.Name;
                sensorObject.value = sensor.Value.ToString();
                Console.WriteLine("Adding sensor : " + sensorObject.name);


                if (lastSensorType == sensor.SensorType) //same kind of sensor as the last one
                {
                    //Add this to the current list of sensors
                    hwStats.sensorTypes[sensorTypeObjectsFoundSoFar].sensors.Add(sensorObject);
                    Console.WriteLine("Sensor types so far : " + sensorTypeObjectsFoundSoFar);
                }
                else //we're on to a new type of sensor
                {
                    sensorTypeObjectsFoundSoFar++; //increment the tracker
                    hwStats.sensorTypes.Add(new Perfbase.SensorType()); //Add new object to list
                    hwStats.sensorTypes[sensorTypeObjectsFoundSoFar].name = sensor.SensorType.ToString(); //Update the name of the next object
                    lastSensorType = sensor.SensorType; //update the tracker
                    Console.WriteLine("New sensor type found!");
                    //Add this to the NEW current list of sensors
                    hwStats.sensorTypes[sensorTypeObjectsFoundSoFar].sensors.Add(sensorObject);
                }
            }

            foreach (IHardware subHardware in hardware.SubHardware)
            {
                ReportPerfSensorTree(subHardware, hwStatList);
            }

            return;
                        
        }

        #endregion

        #region PERFBASE_HELPERS

        /// <summary>
        /// Accepts a HardwareType enum and returns a single string containing
        /// the name of the connected hardware of that type.
        /// </summary>
        /// <param name="hardwareType"></param>
        /// <returns></returns>
        private string GetNameFor(HardwareType hardwareType)
        {
            foreach (IGroup group in groups)
            {
                foreach (IHardware hardware in group.Hardware)
                {
                    if (hardware.HardwareType == hardwareType)
                    {
                        return hardware.Name;
                    }
                }
            }
            //If not found
            return "Not found";
        }

        private Perfbase.GPU[] GetGPUs()
        {
            List<Perfbase.GPU> gpuArray = new List<Perfbase.GPU>();
            foreach (IGroup group in groups)
            {
                foreach (IHardware hardware in group.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.GpuNvidia ||
                        hardware.HardwareType == HardwareType.GpuAti)
                    {
                        Perfbase.GPU gpu = new Perfbase.GPU();
                        gpu.model = hardware.Name;
                        foreach (ISensor sensor in hardware.Sensors)
                        {
                            //
                        }

                        gpu.memory = "";//Add
                        gpuArray.Add(gpu);
                    }
                }
            }
            return gpuArray.ToArray();
        }

        /*
        private string GetTotalRAMSpace()
        {
            foreach (IGroup group in groups)
            {
                foreach (IHardware hardware in group.Hardware)
                {
                    if (hardware.HardwareType == HardwareType.RAM)
                    {
                        return ""; //TODO : get correct ram space
                    }
                }
            }
        }
        */
        #endregion


        public string GetReport() {

      using (StringWriter w = new StringWriter(CultureInfo.InvariantCulture)) {

        w.WriteLine();
        w.WriteLine("Open Hardware Monitor Report");
        w.WriteLine();

        Version version = typeof(Computer).Assembly.GetName().Version;

        NewSection(w);
        w.Write("Version: "); w.WriteLine(version.ToString());
        w.WriteLine();

        NewSection(w);
        w.Write("Common Language Runtime: ");
        w.WriteLine(Environment.Version.ToString());
        w.Write("Operating System: ");
        w.WriteLine(Environment.OSVersion.ToString());
        w.Write("Process Type: ");
        w.WriteLine(IntPtr.Size == 4 ? "32-Bit" : "64-Bit");
        w.WriteLine();

        string r = Ring0.GetReport();
        if (r != null) {
          NewSection(w);
          w.Write(r);
          w.WriteLine();
        }

        NewSection(w);
        w.WriteLine("Sensors");
        w.WriteLine();
        foreach (IGroup group in groups) {
          foreach (IHardware hardware in group.Hardware)
            ReportHardwareSensorTree(hardware, w, "");
        }
        w.WriteLine();

        NewSection(w);
        w.WriteLine("Parameters");
        w.WriteLine();
        foreach (IGroup group in groups) {
          foreach (IHardware hardware in group.Hardware)
            ReportHardwareParameterTree(hardware, w, "");
        }
        w.WriteLine();

        foreach (IGroup group in groups) {
          string report = group.GetReport();
          if (!string.IsNullOrEmpty(report)) {
            NewSection(w);
            w.Write(report);
          }

          IHardware[] hardwareArray = group.Hardware;
          foreach (IHardware hardware in hardwareArray)
            ReportHardware(hardware, w);

        }
        return w.ToString();
      }
    }

    


    public void Close() {      
      if (!open)
        return;

      while (groups.Count > 0) {
        IGroup group = groups[groups.Count - 1];
        Remove(group);         
      } 

      Opcode.Close();
      Ring0.Close();

      this.smbios = null;

      open = false;
    }

    public event HardwareEventHandler HardwareAdded;
    public event HardwareEventHandler HardwareRemoved;

    public void Accept(IVisitor visitor) {
      if (visitor == null)
        throw new ArgumentNullException("visitor");
      visitor.VisitComputer(this);
    }

    public void Traverse(IVisitor visitor) {
      foreach (IGroup group in groups)
        foreach (IHardware hardware in group.Hardware) 
          hardware.Accept(visitor);
    }

    private class Settings : ISettings {

      public bool Contains(string name) {
        return false;
      }

      public void SetValue(string name, string value) { }

      public string GetValue(string name, string value) {
        return value;
      }

      public void Remove(string name) { }
    }
  }
}
