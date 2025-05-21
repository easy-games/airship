using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
using static LuauCore;

namespace Assets.Luau
{
    [Serializable]
    public class BinaryBlob : IEquatable<BinaryBlob>
    {
        public BinaryBlob() {
            data = new byte[] { };
            dataSize = 0;
        }
        public BinaryBlob(byte[] bytes)
        {
            dataSize = bytes.Length;
            data = bytes;
        }
        public long dataSize;
        public byte[] data;

        private Dictionary<object, object> m_cachedDictionary = null;

        public Dictionary<object, object> GetDictionary()
        {
            if (m_cachedDictionary != null)
            {
                return m_cachedDictionary;
            }

            int readPos = 1; //skip the first byte, its a magic key to let us know its a blob
            m_cachedDictionary = Decode(this.data, ref readPos);
            return m_cachedDictionary;
        }

        
        enum keyTypes : byte
        {
            KEY_TERMINATOR = 0,
            KEY_TABLE = 1,
            KEY_BYTENUMBER = 2,
            KEY_SHORTNUMBER = 3,
            KEY_INTNUMBER = 4,
            KEY_FLOATNUMBER = 5,
            KEY_DOUBLENUMBER = 6,
            KEY_SHORTSTRING = 7,
            KEY_LONGSTRING = 8,
            KEY_VECTOR3 = 9,
            KEY_BOOLEANTRUE = 10,
            KEY_BOOLEANFALSE = 11,
            KEY_PODTYPE = 12,
        };

        private static Dictionary<object, object> Decode(byte[] buffer, ref int readPos)
        {
            Dictionary<object, object> dictionary = new();

            while (true)
            {
                byte keyType = readByte(buffer, ref readPos);

                
                if (keyType == (byte)keyTypes.KEY_TERMINATOR)
                {
                    //All done
                    return dictionary;
                }

                object key = null;
                object value = null;

                //All the supported key types ONLY
                switch ((keyTypes)keyType)
                {
                    case keyTypes.KEY_SHORTSTRING:
                        {
                            key = decodeShortStringToLua(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_LONGSTRING:
                        {
                            key = decodeLongStringToLua(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_BYTENUMBER:
                        {
                            key = readByte(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_SHORTNUMBER:
                        {
                            key = readShort(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_INTNUMBER:
                        {
                            key = readInt(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_FLOATNUMBER:
                        {
                            key = readFloat(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_DOUBLENUMBER:
                        {
                            key = readDouble(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_VECTOR3:
                        {
                            key = readVector3(buffer, ref readPos);
                            break;
                        }
                    default:
                        {
                            Debug.LogError("[AirshipNet] Format problem decoding buffer - unknown key type. keyType=" + keyType);
                            return null;
                        }
                }

                byte valueType = readByte(buffer, ref readPos);
                //All the value types
                switch ((keyTypes)valueType)
                {
                    case keyTypes.KEY_SHORTSTRING:
                        {
                            value = decodeShortStringToLua(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_LONGSTRING:
                        {
                            value = decodeLongStringToLua(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_BYTENUMBER:
                        {
                            value = readByte(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_SHORTNUMBER:
                        {
                            value = readShort(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_INTNUMBER:
                        {
                            value = readInt(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_FLOATNUMBER:
                        {
                            value = readFloat(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_DOUBLENUMBER:
                        {
                            value = readDouble(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_VECTOR3:
                        {
                            value = readVector3(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_BOOLEANTRUE:
                        {
                            value = true;
                            break;
                        }
                    case keyTypes.KEY_BOOLEANFALSE:
                        {
                            value = false;
                            break;
                        }
                    case keyTypes.KEY_TABLE:
                        {
                            value = Decode(buffer, ref readPos);
                            break;
                        }
                    case keyTypes.KEY_PODTYPE:
                        {
                            value = readPodType(buffer, ref readPos);
                            break;
                        }
                    default:
                        {
                            Debug.LogError("[AirshipNet] Format problem decoding buffer - unknown value type. valueType=" + valueType);
                            return null;
                        }
                }

                //store it
                dictionary.Add(key, value);
            }
            
        }

        //Read a byte
        private static byte readByte(byte[] buffer, ref int readPos)
        {
            byte value = buffer[readPos];
            readPos += 1;
            return value;
        }

        private static short readShort(byte[] buffer, ref int readPos)
        {
            short value = BitConverter.ToInt16(buffer, readPos);
            readPos += 2;
            return value;
        }

        private static int readInt(byte[] buffer, ref int readPos)
        {
            int value = BitConverter.ToInt32(buffer, readPos);
            readPos += 4;
            return value;
        }

        private static float readFloat(byte[] buffer, ref int readPos)
        {
            float value = BitConverter.ToSingle(buffer, readPos);
            readPos += 4;
            return value;
        }

        private static double readDouble(byte[] buffer, ref int readPos)
        {
            double value = BitConverter.ToDouble(buffer, readPos);
            readPos += 8;
            return value;
        }

        private static Vector3 readVector3(byte[] buffer, ref int readPos)
        {
            Vector3 value = new();
            value.x = readFloat(buffer, ref readPos);
            value.y = readFloat(buffer, ref readPos);
            value.z = readFloat(buffer, ref readPos);
            return value;
        }

        private static object decodeShortStringToLua(byte[] buffer, ref int readPos)
        {
            byte stringLength = readByte(buffer, ref readPos);
            string value = Encoding.UTF8.GetString(buffer, readPos, stringLength);
            readPos += stringLength;
            return value;
        }

        private static object decodeLongStringToLua(byte[] buffer, ref int readPos)
        {
            short stringLength = readShort(buffer, ref readPos);
            string value = Encoding.UTF8.GetString(buffer, readPos, stringLength);
            readPos += stringLength;
            return value;
        }

        unsafe
        private static object readPodType(byte[] buffer, ref int readPos)
        {
         
            int podType = readByte(buffer, ref readPos);
            fixed (byte* pData = buffer)
            {
                IntPtr data = (IntPtr)pData + readPos;

                switch ((PODTYPE)podType)
                {
                    default:
                        {
                            Debug.LogError("[AirshipNet] Unhandled pod type encountered during decode");
                            return null;
                        }

                    case PODTYPE.POD_RAY:
                        {
                            readPos += LuauCore.RaySize();
                            return LuauCore.NewRayFromPointer(data);
                        }
                    case PODTYPE.POD_PLANE:
                        {
                            readPos += LuauCore.PlaneSize();
                            return LuauCore.NewPlaneFromPointer(data);
                        }
                    case PODTYPE.POD_MATRIX:
                        {
                            readPos += LuauCore.MatrixSize();
                            return LuauCore.NewMatrixFromPointer(data);
                        }
                    case PODTYPE.POD_QUATERNION:
                        {
                            readPos += LuauCore.QuaternionSize();
                            return LuauCore.NewQuaternionFromPointer(data);
                        }
                    case PODTYPE.POD_VECTOR2:
                        {
                            readPos = LuauCore.Vector2Size();
                            return LuauCore.NewVector2FromPointer(data);
                        }
                    case PODTYPE.POD_VECTOR4:
                        {
                            readPos = LuauCore.Vector4Size();
                            return LuauCore.NewVector4FromPointer(data);
                        }
                    case PODTYPE.POD_COLOR:
                        {
                            readPos += LuauCore.ColorSize();
                            return LuauCore.NewColorFromPointer(data);
                        }
                    case PODTYPE.POD_BINARYBLOB:
                        {
                            int sizeOfBlob = readInt(buffer, ref readPos);
                            byte[] blobBuffer = new byte[sizeOfBlob + 4];
                            Marshal.Copy(data, blobBuffer, 0, sizeOfBlob + 4);

                            return new BinaryBlob(blobBuffer);
                        }
                       
                }
            }
            
 
        }

        public bool Equals(BinaryBlob other) {
            return this.dataSize == other?.dataSize;
        }
    }
}
