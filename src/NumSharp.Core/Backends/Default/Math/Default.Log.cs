﻿/*
This file was generated by template ../NDArray.Elementwise.tt
In case you want to do some changes do the following 

1 ) adapt the tt file
2 ) execute powershell file "GenerateCode.ps1" on root level

*/
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Numerics;
using NumSharp.Shared;

namespace NumSharp.Backends
{
    public partial class DefaultEngine
    {
        public override NDArray Log(NDArray x)
        {
            return null;
            //var logArray = new NDArray(x.dtype, x.shape);

            //Array dataSysArr = x.Array;
            //Array logDataSysArr = logArray.Array;

            //switch (logDataSysArr)
            //{
            //    case double[] logData:
            //        {
            //            double[] npData = dataSysArr as double[];

            //            for (int idx = 0; idx < npData.Length; idx++)
            //            {
            //                logData[idx] = Math.Log(npData[idx]);
            //            }
            //            break;
            //        }
            //    case float[] logData:
            //        {
            //            float[] npData = dataSysArr as float[];

            //            for (int idx = 0; idx < npData.Length; idx++)
            //            {
            //                // boxing necessary because Math.log just for double
            //                logData[idx] = (float)Math.Log(npData[idx]);
            //            }
            //            break;
            //        }
            //    case Complex[] logData:
            //        {
            //            Complex[] npData = dataSysArr as Complex[];

            //            for (int idx = 0; idx < npData.Length; idx++)
            //            {
            //                logData[idx] = Complex.Log(npData[idx]);
            //            }
            //            break;
            //        }
            //    default:
            //        {
            //            throw new IncorrectTypeException();
            //        }

            //}
            //return logArray;
        }
    }
}

