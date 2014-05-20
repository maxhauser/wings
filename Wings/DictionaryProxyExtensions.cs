using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;


namespace Wings
{
    using System.Linq.Expressions;
    using Conversion = Func<string, Type, object, object>;
    using Factory = Func<IDictionary<string, object>, Func<string, Type, object, object>, object>;

    class FactoryBuilder
    {
        const MethodAttributes accessibilityFlags = MethodAttributes.Public | MethodAttributes.Private | MethodAttributes.Assembly | MethodAttributes.Family | MethodAttributes.FamANDAssem | MethodAttributes.FamORAssem;

        readonly static MethodInfo GetterHelperMethod = typeof(InternalDictionaryProxyHelper).GetMethod("Getter", BindingFlags.Static | BindingFlags.Public);
        readonly static MethodInfo DelegateGetterHelperMethod = typeof(InternalDictionaryProxyHelper).GetMethod("DelegateGetter", BindingFlags.Static | BindingFlags.Public);
        readonly static MethodInfo SetterHelperMethod = typeof(InternalDictionaryProxyHelper).GetMethod("Setter", BindingFlags.Static | BindingFlags.Public);
        readonly static MethodInfo GetTypeFromHandleMethod = typeof(Type).GetMethod("GetTypeFromHandle");
        readonly static MethodInfo GetDefaultMethod = typeof(InternalDictionaryProxyHelper).GetMethod("GetDefault", BindingFlags.Static | BindingFlags.Public);

        readonly static AssemblyBuilder assemblyBuilder;
        readonly static ModuleBuilder moduleBuilder;

        TypeBuilder typeBuilder;
        FieldBuilder dictField;
        FieldBuilder conversionField;

        static FactoryBuilder()
        {
            assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("DictionaryProxies"), AssemblyBuilderAccess.RunAndCollect);
            moduleBuilder = assemblyBuilder.DefineDynamicModule("main");
        }

        public Factory CreateFactory(Type type)
        {
            if (!type.IsAbstract || !(type.IsPublic || type.IsNestedPublic))
                throw new InvalidOperationException("Can only proxy public interfaces or abstract classes.");

            var baseType = type.IsInterface ? typeof(object) : type;

            this.typeBuilder = moduleBuilder.DefineType(type.Name + "Proxy",
                TypeAttributes.AutoClass | TypeAttributes.BeforeFieldInit | TypeAttributes.Class | TypeAttributes.Public,
                baseType, type.IsInterface ? new[] { type } : Type.EmptyTypes);

            this.dictField = typeBuilder.DefineField("dict", typeof(IDictionary<string, object>), FieldAttributes.InitOnly | FieldAttributes.Private);
            this.conversionField = typeBuilder.DefineField("conversion", typeof(Conversion), FieldAttributes.InitOnly | FieldAttributes.Private);

            var ctor = ImplementConstructor(baseType);

            ImplementCreateMethod(type, ctor);

            var members = type.GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var member in members)
            {
                var property = member as PropertyInfo;
                if (property != null)
                {
                    if (!property.GetMethod.IsAbstract)
                        continue;
                    ImplementProperty(property);
                    continue;
                }

                var method = member as MethodInfo;
                if (method != null)
                {
                    if (method.IsSpecialName || !method.IsAbstract)
                        continue;
                    ImplementMethod(method);
                    continue;
                }

                if (member is ConstructorInfo)
                    continue;

                throw new NotSupportedException("Implementation of " + member + " not supported.");
            }

            var createdType = this.typeBuilder.CreateType();

            return (Factory)createdType.GetMethod("Create", BindingFlags.Static | BindingFlags.Public).CreateDelegate(typeof(Factory));
        }

        void ImplementMethod(MethodInfo method)
        {
            var accessibility = method.Attributes & accessibilityFlags;
            var parameters = method.GetParameters();
            var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
            var methodBuilder = this.typeBuilder.DefineMethod(method.Name, MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual | accessibility,
                method.ReturnType, parameterTypes);

            var il = methodBuilder.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, dictField);
            il.Emit(OpCodes.Ldstr, method.Name);
            il.Emit(OpCodes.Call, DelegateGetterHelperMethod);

            var hasReturnValue = method.ReturnType != typeof(void);
            Type delegateType;
            if (hasReturnValue)
            {
                var delegateArgumentTypes = new Type[parameterTypes.Length + 1];
                Array.Copy(parameterTypes, delegateArgumentTypes, parameterTypes.Length);
                delegateArgumentTypes[parameterTypes.Length] = method.ReturnType;
                delegateType = Expression.GetFuncType(delegateArgumentTypes);
            }
            else
            {
                delegateType = Expression.GetActionType(parameterTypes);
            }

            for (int i = 0; i < parameterTypes.Length; i++)
                LoadArg(il, (byte)(i + 1));

            il.Emit(OpCodes.Tailcall);
            il.EmitCall(OpCodes.Callvirt, delegateType.GetMethod("Invoke"), null);
            il.Emit(OpCodes.Ret);
        }

        void LoadArg(ILGenerator il, byte ix)
        {
            switch (ix)
            {
                case 0: il.Emit(OpCodes.Ldarg_0); break;
                case 1: il.Emit(OpCodes.Ldarg_1); break;
                case 2: il.Emit(OpCodes.Ldarg_2); break;
                case 3: il.Emit(OpCodes.Ldarg_3); break;
                default:
                    il.Emit(OpCodes.Ldarg_S, ix); break;
            }
        }

        void ImplementCreateMethod(Type type, ConstructorBuilder ctor)
        {
            var createMethod = typeBuilder.DefineMethod("Create", MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.Public, type, new[] { typeof(IDictionary<string, object>), typeof(Conversion) });
            var il = createMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Newobj, ctor);
            il.Emit(OpCodes.Ret);
        }

        ConstructorBuilder ImplementConstructor(Type baseType)
        {
            var ctor = typeBuilder.DefineConstructor(MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.RTSpecialName | MethodAttributes.SpecialName,
                CallingConventions.HasThis, new[] { typeof(IDictionary<string, object>), typeof(Conversion) });
            var il = ctor.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);

            var ctors = baseType.GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            ConstructorInfo baseCtor = null;
            foreach (var ct in ctors)
            {
                var args = ct.GetParameters();
                if (args.Length == 1 && typeof(IDictionary<string, object>).Equals(args[0].ParameterType))
                {
                    baseCtor = ct;
                    il.Emit(OpCodes.Ldarg_1);
                    break;
                }
                if (args.Length == 0)
                    baseCtor = ct;
            }
            if (baseCtor == null)
                throw new InvalidOperationException("Type has no suitable constructor.");

            il.Emit(OpCodes.Call, baseCtor);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Stfld, dictField);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Stfld, conversionField);

            il.Emit(OpCodes.Ret);

            return ctor;
        }

        void ImplementProperty(PropertyInfo property)
        {
            var propertyBuilder = this.typeBuilder.DefineProperty(property.Name, PropertyAttributes.None, property.PropertyType, null);

            if (property.CanRead)
            {
                var getMethod = property.GetMethod;
                var accessibility = getMethod.Attributes & accessibilityFlags;
                var getterMethod = this.typeBuilder.DefineMethod("get_" + property.Name, MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual | accessibility,
                    property.PropertyType, Type.EmptyTypes);
                var il = getterMethod.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, dictField);
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, conversionField);
                il.Emit(OpCodes.Ldstr, property.Name);
                il.Emit(OpCodes.Ldtoken, property.PropertyType);
                il.Emit(OpCodes.Call, GetTypeFromHandleMethod);
                il.Emit(OpCodes.Call, GetterHelperMethod);

                il.Emit(OpCodes.Dup);
                il.Emit(OpCodes.Ldnull);
                var retdefault = il.DefineLabel();
                il.Emit(OpCodes.Beq_S, retdefault);
                il.Emit(OpCodes.Unbox_Any, property.PropertyType);
                il.Emit(OpCodes.Ret);

                il.MarkLabel(retdefault);
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Call, GetDefaultMethod.MakeGenericMethod(property.PropertyType));
                il.Emit(OpCodes.Ret);
            }

            if (property.CanWrite)
            {
                var setMethod = property.SetMethod;
                var accessibility = setMethod.Attributes & accessibilityFlags;
                var setterMethod = this.typeBuilder.DefineMethod("set_" + property.Name, MethodAttributes.Final | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Virtual | accessibility,
                    typeof(void), new[] { property.PropertyType });
                var il = setterMethod.GetILGenerator();

                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Ldfld, dictField);
                il.Emit(OpCodes.Ldstr, property.Name);
                il.Emit(OpCodes.Ldarg_1);
                if (property.PropertyType.IsValueType)
                    il.Emit(OpCodes.Box, property.PropertyType);

                il.Emit(OpCodes.Call, SetterHelperMethod);
                il.Emit(OpCodes.Ret);
            }
        }
    }

    public static class InternalDictionaryProxyHelper
    {
        public static T GetDefault<T>()
        {
            return default(T);
        }

        public static object DelegateGetter(IDictionary<string, object> dict, string name)
        {
            object value;
            if (!dict.TryGetValue(name, out value) || value == null)
                throw new NotImplementedException(name);

            return value;
        }

        public static object Getter(IDictionary<string, object> dict, Conversion conversion, string name, Type type)
        {
            object value;
            if (!dict.TryGetValue(name, out value) || value == null)
                return null;

            var valueType = value.GetType();
            if (type.IsAssignableFrom(valueType))
                return value;

            if (conversion == null)
                throw new InvalidCastException("Cannot cast from " + valueType.Name + " to " + type.Name);

            return conversion(name, type, value);
        }

        public static void Setter(IDictionary<string, object> dict, string name, object value)
        {
            dict[name] = value;
        }
    }

    public static class DictionaryProxyExtensions
    {
        readonly static object syncLock = new object();
        readonly static Dictionary<Type, Factory> proxyCache = new Dictionary<Type, Factory>();
        readonly static FactoryBuilder builder = new FactoryBuilder();

        public static T As<T>(this IDictionary<string, object> dict)
        {
            return (T)As(dict, typeof(T), null);
        }

        public static object As(this IDictionary<string, object> dict, Type type)
        {
            return As(dict, type, null);
        }

        public static T As<T>(this IDictionary<string, object> dict, Conversion conversion)
        {
            return (T)As(dict, typeof(T), conversion);
        }

        public static object As(this IDictionary<string, object> dict, Type type, Conversion conversion)
        {
            var factory = GetProxyFactory(type);
            return factory(dict, conversion);
        }

        static Factory GetProxyFactory(Type type)
        {
            Factory factory;
            lock (syncLock)
            {
                if (!proxyCache.TryGetValue(type, out factory))
                {
                    factory = builder.CreateFactory(type);
                    proxyCache.Add(type, factory);
                }
            }
            return factory;
        }
    }
}
