using Avalonia;
using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace AvaloniaCompositionScrollExample.Scroll
{
    // Only for suppress SynchronizeCompositionProperties
    internal static class HackClassBuilder
    {
        private static Type _hackType;

        public static ScrollProxy CreateClass(CompositionScrollDecorator scrollDecorator)
        {
            if (_hackType == null)
                _hackType = CompileHackType();

            return Activator.CreateInstance(_hackType, scrollDecorator) as ScrollProxy;
        }

        private static Type CompileHackType()
        {
            var typeSignature = "HackType";
            var an = new AssemblyName(typeSignature);

            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.Run);
            var ignoresAccessChecksTo = new CustomAttributeBuilder
                (
                typeof(IgnoresAccessChecksToAttribute).GetConstructor(new Type[] { typeof(string) }),
                new object[] { typeof(Visual).Assembly.GetName().Name }
                );
            var ignoresAccessChecksTo2 = new CustomAttributeBuilder
               (
               typeof(IgnoresAccessChecksToAttribute).GetConstructor(new Type[] { typeof(string) }),
               new object[] { typeof(HackClassBuilder).Assembly.GetName().Name }
               );

            assemblyBuilder.SetCustomAttribute(ignoresAccessChecksTo);
            assemblyBuilder.SetCustomAttribute(ignoresAccessChecksTo2);

            ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

            TypeBuilder tb = moduleBuilder.DefineType(typeSignature,
                    TypeAttributes.Public |
                    TypeAttributes.Class |
                    TypeAttributes.AutoClass |
                    TypeAttributes.AnsiClass |
                    TypeAttributes.BeforeFieldInit |
                    TypeAttributes.AutoLayout,
                    typeof(ScrollProxy));

            var baseConstructor = typeof(ScrollProxy).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy, null, new[] { typeof(CompositionScrollDecorator) }, null);

            ConstructorBuilder constructor = tb.DefineConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, CallingConventions.Standard, new[] { typeof(CompositionScrollDecorator) });
            ILGenerator constructorIl = constructor.GetILGenerator();

            constructorIl.Emit(OpCodes.Ldarg_0);
            constructorIl.Emit(OpCodes.Ldarg_1);
            constructorIl.Emit(OpCodes.Call, baseConstructor);

            constructorIl.Emit(OpCodes.Ret);

            MethodBuilder methodBuilder = tb.DefineMethod("SynchronizeCompositionProperties",
                MethodAttributes.Private | MethodAttributes.HideBySig |
                MethodAttributes.NewSlot | MethodAttributes.Virtual |
                MethodAttributes.Final,
                null,
                Type.EmptyTypes);
            ILGenerator il = methodBuilder.GetILGenerator();
            il.Emit(OpCodes.Ret);

            var SynchronizeCompositionPropertiesMethodInfo =
                typeof(Visual).GetMethod("SynchronizeCompositionProperties", BindingFlags.Instance | BindingFlags.NonPublic);

            tb.DefineMethodOverride(methodBuilder, SynchronizeCompositionPropertiesMethodInfo);

            return tb.CreateType();
        }
    }
}

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName)
        {
            AssemblyName = assemblyName;
        }

        public string AssemblyName { get; }
    }
}