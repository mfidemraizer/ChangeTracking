﻿using Castle.DynamicProxy;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ChangeTracking
{
    public static class Core
    {
        private static ProxyGenerator _ProxyGenerator;
        private static ProxyGenerationOptions _Options;

        static Core()
        {
            _ProxyGenerator = new ProxyGenerator();
            _Options = new ProxyGenerationOptions
            {
                Hook = new ChangeTrackingProxyGenerationHook(),
                Selector = new ChangeTrackingInterceptorSelector()
            };
        }

        internal static object AsTrackableCollectionChild(Type type, object target, bool makeComplexPropertiesTrackable, bool makeCollectionPropertiesTrackable)
        {
            Type genericArgument = type.GetGenericArguments().First();
            return _ProxyGenerator.CreateInterfaceProxyWithTarget(typeof(IList<>).MakeGenericType(genericArgument),
                        new[] { typeof(IChangeTrackableCollection<>).MakeGenericType(genericArgument), typeof(IBindingList) },
                        target,
                        _Options,
                        (IInterceptor)CreateInstance(typeof(ChangeTrackingCollectionInterceptor<>).MakeGenericType(genericArgument), target, makeComplexPropertiesTrackable, makeCollectionPropertiesTrackable));
        }

        internal static object AsTrackableChild(Type type, object target, Action<object> notifyParentItemCanceled, bool makeComplexPropertiesTrackable, bool makeCollectionPropertiesTrackable)
        {
            return _ProxyGenerator.CreateClassProxyWithTarget(type,
                         new[] { typeof(IChangeTrackableInternal), typeof(IChangeTrackable<>).MakeGenericType(type), typeof(IChangeTrackingManager), typeof(IComplexPropertyTrackable), typeof(ICollectionPropertyTrackable), typeof(IEditableObject), typeof(System.ComponentModel.INotifyPropertyChanged) },
                         target,
                         _Options,
                         (IInterceptor)CreateInstance(typeof(NotifyPropertyChangedInterceptor<>).MakeGenericType(type)),
                         (IInterceptor)CreateInstance(typeof(ChangeTrackingInterceptor<>).MakeGenericType(type), ChangeStatus.Unchanged),
                         (IInterceptor)CreateInstance(typeof(EditableObjectInterceptor<>).MakeGenericType(type), notifyParentItemCanceled),
                         (IInterceptor)CreateInstance(typeof(ComplexPropertyInterceptor<>).MakeGenericType(type), makeComplexPropertiesTrackable, makeCollectionPropertiesTrackable),
                         (IInterceptor)CreateInstance(typeof(CollectionPropertyInterceptor<>).MakeGenericType(type), makeComplexPropertiesTrackable, makeCollectionPropertiesTrackable));
        }

        private static object CreateInstance(Type type, params object[] args)
        {
            return Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, args, null);
        }

        public static T AsTrackable<T>(this T target, ChangeStatus status = ChangeStatus.Unchanged, bool makeComplexPropertiesTrackable = true, bool makeCollectionPropertiesTrackable = true) where T : class
        {
            return AsTrackable(target, status, null, makeComplexPropertiesTrackable, makeCollectionPropertiesTrackable);
        }

        internal static T AsTrackable<T>(this T target, ChangeStatus status, Action<T> notifyParentListItemCanceled, bool makeComplexPropertiesTrackable, bool makeCollectionPropertiesTrackable) where T : class
        {
            //if T was ICollection<T> it would of gone to one of the other overloads
            if (target as ICollection != null)
            {
                throw new InvalidOperationException("Only IList<T>, List<T> and ICollection<T> are supported");
            }

            object proxy = _ProxyGenerator.CreateClassProxyWithTarget(typeof(T),
                new[] { typeof(IChangeTrackableInternal), typeof(IChangeTrackable<T>), typeof(IChangeTrackingManager), typeof(IComplexPropertyTrackable), typeof(ICollectionPropertyTrackable), typeof(IEditableObject), typeof(System.ComponentModel.INotifyPropertyChanged) },
                target, 
                _Options,
                new NotifyPropertyChangedInterceptor<T>(), 
                new ChangeTrackingInterceptor<T>(status), 
                new EditableObjectInterceptor<T>(notifyParentListItemCanceled), 
                new ComplexPropertyInterceptor<T>(makeComplexPropertiesTrackable, makeCollectionPropertiesTrackable), 
                new CollectionPropertyInterceptor<T>(makeComplexPropertiesTrackable, makeCollectionPropertiesTrackable));
            return (T)proxy;
        }

        public static ICollection<T> AsTrackable<T>(this System.Collections.ObjectModel.Collection<T> target) where T : class
        {
            return AsTrackable(target, true, true);
        }

        public static ICollection<T> AsTrackable<T>(this System.Collections.ObjectModel.Collection<T> target, bool makeComplexPropertiesTrackable = true, bool makeCollectionPropertiesTrackable = true) where T : class
        {
            return ((ICollection<T>)target).AsTrackable(makeComplexPropertiesTrackable, makeCollectionPropertiesTrackable);
        }

        public static ICollection<T> AsTrackable<T>(this ICollection<T> target) where T : class
        {
            return AsTrackable(target, true, true);
        }

        public static ICollection<T> AsTrackable<T>(this ICollection<T> target, bool makeComplexPropertiesTrackable, bool makeCollectionPropertiesTrackable) where T : class
        {
            var list = target as IList<T>;
            if (target == null)
            {
                list = target.ToList();
            }
            return list.AsTrackable(makeComplexPropertiesTrackable, makeCollectionPropertiesTrackable);
        }

        public static IList<T> AsTrackable<T>(this List<T> target) where T : class
        {
            return AsTrackable(target, true, true);
        }

        public static IList<T> AsTrackable<T>(this List<T> target, bool makeComplexPropertiesTrackable, bool makeCollectionPropertiesTrackable) where T : class
        {
            return ((IList<T>)target).AsTrackable(makeComplexPropertiesTrackable, makeCollectionPropertiesTrackable);
        }

        public static IList<T> AsTrackable<T>(this IList<T> target) where T : class
        {
            return AsTrackable(target, true, true);
        }

        public static IList<T> AsTrackable<T>(this IList<T> target, bool makeComplexPropertiesTrackable, bool makeCollectionPropertiesTrackable) where T : class
        {
            if (target.OfType<IChangeTrackable<T>>().Any(ct => ct.ChangeTrackingStatus != ChangeStatus.Unchanged))
            {
                throw new InvalidOperationException("some items in the collection are already being tracked");
            }
            object proxy = _ProxyGenerator.CreateInterfaceProxyWithTarget(typeof(IList<T>),
                new[] { typeof(IChangeTrackableCollection<T>), typeof(IBindingList), typeof(ICancelAddNew) }, target, _Options, new ChangeTrackingCollectionInterceptor<T>(target, makeComplexPropertiesTrackable, makeCollectionPropertiesTrackable));
            return (IList<T>)proxy;
        }
    }
}
