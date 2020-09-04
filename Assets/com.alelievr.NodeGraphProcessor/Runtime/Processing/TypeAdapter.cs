using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GraphProcessor
{
    /// <summary>
    /// Implement this interface to use the inside your class to define type convertions to use inside the graph.
    /// Example:
    /// <code>
    /// public class CustomConvertions : ITypeAdapter
    /// {
    ///     public static Vector4 ConvertFloatToVector(float from) => new Vector4(from, from, from, from);
    ///     ...
    /// }
    /// </code>
    /// </summary>
    public abstract class ITypeAdapter // TODO: turn this back into an interface when we have C# 8
    {
        public virtual IEnumerable<(Type, Type)> GetIncompatibleTypes() { yield break; }
    }

    public static class TypeAdapter
    {
        static Dictionary< (Type from, Type to), Func<object, object> > adapters          = new Dictionary< (Type, Type), Func<object, object> >();
        static Dictionary< (Type from, Type to), MethodInfo >           adapterMethods    = new Dictionary< (Type, Type), MethodInfo >();
        static List< (Type from, Type to)>                              incompatibleTypes = new List<( Type from, Type to) >();

        [System.NonSerialized]
        static bool adaptersLoaded = false;
#if !ENABLE_IL2CPP
        static Func<object, object> ConvertTypeMethodHelper<TParam, TReturn>(MethodInfo method)
        {
            // Convert the slow MethodInfo into a fast, strongly typed, open delegate
            Func<TParam, TReturn> func = (Func<TParam, TReturn>)Delegate.CreateDelegate
                (typeof(Func<TParam, TReturn>), method);

            // Now create a more weakly typed delegate which will call the strongly typed one
            Func<object, object> ret = (object param) => func((TParam)param);
            return ret;
        }
#endif

        static void LoadAllAdapters()
        {
            foreach (Type type in AppDomain.CurrentDomain.GetAllTypes())
            {
                if (typeof(ITypeAdapter).IsAssignableFrom(type))
                {
                    if (type.IsAbstract)
                        continue;

                    var adapter = Activator.CreateInstance(type) as ITypeAdapter;
                    if (adapter != null)
                    {
                        foreach (var types in adapter.GetIncompatibleTypes())
                        {
                            incompatibleTypes.Add((types.Item1, types.Item2));
                            incompatibleTypes.Add((types.Item2, types.Item1));
                        }
                    }

                    foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                    {
                        if (method.GetParameters().Length != 1)
                        {
                            Debug.LogError($"Ignoring convertion method {method} because it does not have exactly one parameter");
                            continue;
                        }
                        if (method.ReturnType == typeof(void))
                        {
                            Debug.LogError($"Ignoring convertion method {method} because it does not returns anything");
                            continue;
                        }
                        Type from = method.GetParameters()[0].ParameterType;
                        Type to = method.ReturnType;

                        try {
                            
#if ENABLE_IL2CPP
                            // IL2CPP doesn't suport calling generic functions via reflection (AOT can't generate templated code)
                            Func<object, object> r = (object param) => { return (object)method.Invoke(null, new object[]{ param }); };
#else
                            MethodInfo genericHelper = typeof(TypeAdapter).GetMethod("ConvertTypeMethodHelper", 
                                BindingFlags.Static | BindingFlags.NonPublic);
                            
                            // Now supply the type arguments
                            MethodInfo constructedHelper = genericHelper.MakeGenericMethod(from, to);

                            object ret = constructedHelper.Invoke(null, new object[] {method});

                            var r = (Func<object, object>) ret;
#endif

                            adapters.Add((method.GetParameters()[0].ParameterType, method.ReturnType), r);
                            adapterMethods.Add((method.GetParameters()[0].ParameterType, method.ReturnType), method);
                        } catch (Exception e) {
                            Debug.LogError($"Failed to load the type convertion method: {method}\n{e}");
                        }
                    }
                }
            }

            // Ensure that the dictionary contains all the convertions in both ways
            // ex: float to vector but no vector to float
            foreach (var kp in adapters)
            {
                if (!adapters.ContainsKey((kp.Key.to, kp.Key.from)))
                    Debug.LogError($"Missing convertion method. There is one for {kp.Key.from} to {kp.Key.to} but not for {kp.Key.to} to {kp.Key.from}");
            }

            adaptersLoaded = true;
        }
        
        public static bool AreIncompatible(Type from, Type to)
        {
            if (incompatibleTypes.Any((k) => k.from == from && k.to == to))
                return true;
            return false;
        }

        public static bool AreAssignable(Type from, Type to)
        {
            if (!adaptersLoaded)
                LoadAllAdapters();
            
            if (AreIncompatible(from, to))
                return false;

            if(from.Namespace == "System.Collections.Generic" && !to.Name.StartsWith("List"))
            {
                var split = from.ToString().Split('[');
                var stringType = split[1].Substring(0, split[1].Length - 1);

                if(stringType.Split('.')[0] == "UnityEngine")
                {
                    from = Type.GetType(stringType + ", UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                }
                else if(Type.GetType(stringType) != null)
                {
                    from = Type.GetType(stringType);
                }
            }
            
            if(to.Namespace == "System.Collections.Generic" && !to.Name.StartsWith("List"))
            {
                var split = to.ToString().Split('[');
                var stringType = split[1].Substring(0, split[1].Length - 1);

                if(stringType.Split('.')[0] == "UnityEngine")
                {
                    to = Type.GetType(stringType + ", UnityEngine.CoreModule, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null");
                }
                else if(Type.GetType(stringType) != null)
                {
                    to = Type.GetType(stringType);
                }
            }
            
            return adapters.ContainsKey((from, to));
        }

        public static MethodInfo GetConvertionMethod(Type from, Type to) => adapterMethods[(from, to)];

        public static object Convert(object from, Type targetType)
        {
            if (!adaptersLoaded)
                LoadAllAdapters();

            Func<object, object> convertionFunction;
            if (adapters.TryGetValue((from.GetType(), targetType), out convertionFunction))
                return convertionFunction?.Invoke(from);

            return null;
        }
    }
}