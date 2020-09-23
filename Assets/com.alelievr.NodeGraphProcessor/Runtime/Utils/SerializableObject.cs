using System;
using UnityEngine;
using System.Globalization;
using UnityEngine.AddressableAssets;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GraphProcessor
{
    [System.Serializable]
    public class SerializableObject : ISerializationCallbackReceiver
    {
        [System.Serializable]
        class ObjectWrapper
        {
            public UnityEngine.Object value;
        }
        
        #region Supported Objects
        
        [Serializable]
        public class SerializedValueBase {}

        [Serializable]
        public class SerializedObject : SerializedValueBase
        {
            public UnityEngine.Object value;
        }

        [Serializable]
        public class SerializedAnimationCurve : SerializedValueBase
        {
            public AnimationCurve value;
        }

        [Serializable]
        public class SerializedVector2 : SerializedValueBase
        {
            public Vector2 value;
        }

        [Serializable]
        public class SerializedVector3 : SerializedValueBase
        {
            public Vector3 value;
        }

        [Serializable]
        public class SerializedColor : SerializedValueBase
        {
            public Color value;
        }

        [Serializable]
        public class SerializedAsset : SerializedValueBase
        {
            public AssetReferenceGameObject value;
        }
        
        [SerializeReference] public SerializedValueBase serializedObjectValue;
        
        #endregion

        public string serializedType;
        public string serializedName;
        public string serializedValue;

        public object value;

        public SerializableObject(object value, Type type, string name = null)
        {
            this.value = value;
            this.serializedName = name;
            this.serializedType = type.AssemblyQualifiedName;
        }

        public void OnAfterDeserialize()
        {
            if (String.IsNullOrEmpty(serializedType))
            {
                Debug.LogError("Can't deserialize the object from null type");
                return;
            }

            Type type = Type.GetType(serializedType);

            if (type.IsPrimitive)
            {
                if (string.IsNullOrEmpty(serializedValue))
                    value = Activator.CreateInstance(type);
                else
                    value = Convert.ChangeType(serializedValue, type, CultureInfo.InvariantCulture);
            }
            else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                value = (serializedObjectValue as SerializedObject)?.value;
            }
            else if (type == typeof(string))
                value = serializedValue.Length > 1 ? serializedValue.Substring(1, serializedValue.Length - 2).Replace("\\\"", "\"") : "";
            else
            {
                try {
                    if(typeof(AnimationCurve).IsAssignableFrom(type))
                    {
                        value = (serializedObjectValue as SerializedAnimationCurve)?.value;
                    }
                    else if(typeof(Vector2).IsAssignableFrom(type))
                    {
                        value = (serializedObjectValue as SerializedVector2)?.value;
                    }
                    else if(typeof(Vector3).IsAssignableFrom(type))
                    {
                        value = (serializedObjectValue as SerializedVector3)?.value;
                    }
                    else if(typeof(Color).IsAssignableFrom(type))
                    {
                        value = (serializedObjectValue as SerializedColor)?.value;
                    }
                    else if(typeof(AssetReferenceGameObject).IsAssignableFrom(type))
                    {
                        value = (serializedObjectValue as SerializedAsset)?.value;
                    }
                    else
                    {
                        value = Activator.CreateInstance(type);
                        JsonUtility.FromJsonOverwrite(serializedValue, value);
                    }
                } catch (Exception e){
                    Debug.LogError(e);
                    Debug.LogError("Can't serialize type " + serializedType);
                }
            }
        }

        public void OnBeforeSerialize()
        {
            if (value == null)
                return ;

            serializedType = value.GetType().AssemblyQualifiedName;

            if (value.GetType().IsPrimitive)
                serializedValue = value.ToString();
            else if (value is UnityEngine.Object) //type is a unity object
            {
                if ((value as UnityEngine.Object) == null)
                    return ;
                
                serializedObjectValue = new SerializedObject { value = value as UnityEngine.Object };
            }
            else if (value is string)
                serializedValue = "\"" + ((string)value).Replace("\"", "\\\"") + "\"";
            else
            {
                try {
                    if(value is AnimationCurve)
                    {
                        serializedObjectValue = new SerializedAnimationCurve { value = value as AnimationCurve};
                    } else if(value is Color)
                    {
                        serializedObjectValue = new SerializedColor { value = value as Color? ?? new Color()};
                    } else if(value is Vector2)
                    {
                        serializedObjectValue = new SerializedVector2 { value = (Vector2) value};
                    }
                    else if(value is Vector3)
                    {
                        serializedObjectValue = new SerializedVector3 { value = value as Vector3? ?? new Vector3()};
                    }
                    else if(value is AssetReferenceGameObject)
                    {
                        serializedObjectValue = new SerializedAsset { value = value as AssetReferenceGameObject};
                    }
                    else
                    {
                        serializedValue = JsonUtility.ToJson(value);
                        if(String.IsNullOrEmpty(serializedValue))
                            throw new Exception();
                    }
                } catch {
                    Debug.LogError("Can't serialize type " + serializedType);
                }
            }
        }
    }
}