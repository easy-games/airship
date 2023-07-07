using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace VeryAnimation
{
    public class MusclePropertyName
    {
        public string[] Names { get; private set; }
        public string[] PropertyNames { get; private set; }
        public Dictionary<string, int> PropertyNameDic { get; private set; }

        public MusclePropertyName()
        {
            Names = HumanTrait.MuscleName;
            PropertyNames = new string[Names.Length];
            PropertyNameDic = new Dictionary<string, int>(Names.Length);
            for (int i = 0; i < Names.Length; i++)
            {
                if (Names[i].EndsWith("Stretched"))
                {
                    var splits = Names[i].Split(' ');
                    PropertyNames[i] = string.Format("{0}Hand.{1}.{2} {3}", splits[0], splits[1], splits[2], splits[3]);
                }
                else if (Names[i].EndsWith("Spread"))
                {
                    var splits = Names[i].Split(' ');
                    PropertyNames[i] = string.Format("{0}Hand.{1}.{2}", splits[0], splits[1], splits[2]);
                }
                else
                {
                    PropertyNames[i] = Names[i];
                }
                PropertyNameDic.Add(PropertyNames[i], i);
            }
        }
    }
}
