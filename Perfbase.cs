using System;
using System.Collections.Generic;
using System.Text;

namespace Perfbase
{
    public class Perfbase 
    {

    }

    public class HardwareStats
    {
        public string name;                 //Name of the device
        public List<SensorType> sensorTypes = new List<SensorType>();    //Contains an array of all the different
                                            //types of sensors this hardware has
                                            //which contains all those specific sensors.
    }

    /// <summary>
    /// This is different from OpenHardware.ISensor.SensorType
    /// Contains a name and multiple sensors of that type.
    /// </summary>
    public class SensorType
    {
        public string name;
        public List<Sensor> sensors = new List<Sensor>();
    }

    /// <summary>
    /// This is different from OpenHardware.ISensor.Sensor
    /// Contains a name and a value for that sensor
    /// </summary>
    public class Sensor
    {
        public string name;
        public string value;
    }

    public class Config
    {
        public string computerName; //Name of the machine
        public string os; //Operating System
        public string osType; //Process Type: 64-Bit/32-Bit
        public string motherboardModel;
        public string cpuModel;
        public string ramSpace; //total RAM installed on this system
        public GPU[] gpu; //array of all GPUs
        public string totalHDDSpace; //Total HDD space on this machine
        public HDD[] hdd; //array of disks on this machine
        
        //Todo : create a constructor to avoid missing values

    }

    public class GPU
    {
        public string model; //Model of this GPU
        public string memory; //Memory in this GPU
    }

    public class HDD
    {
        public string model; //Model of this HDD
        public string space; //Disk space in this HDD
    }


}
