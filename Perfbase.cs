using System;
using System.Collections.Generic;
using System.Text;

namespace Perfbase
{
    public class Perfbase 
    {

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
