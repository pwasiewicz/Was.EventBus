﻿namespace Was.EventBus.Invokers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Castle.DynamicProxy;

    public class MultiInterfaceProxy : IInterceptor
    {
        private readonly IImplementationProvider implementationProvider;
        private readonly Type eventType;

        private IEnumerable<IEvent> events;
        private IEnumerable<MethodInfo> eventMethods;

        private MultiInterfaceProxy(Type eventType, IImplementationProvider implementationProvider, bool resolveEventsImmediately)
        {
            this.implementationProvider = implementationProvider;
            this.eventType = eventType;

            if (resolveEventsImmediately)
            {
                this.events = this.implementationProvider.For(this.eventType).ToList();
            }
        }

        private IEnumerable<IEvent> Events
        {
            get
            {
                return this.events ?? (this.events = this.implementationProvider.For(this.eventType).ToList());
            }
        }

        public static object For(Type eventType, IImplementationProvider implementationProvider, bool resolveEventsImmediately = false)
        {
            if (!eventType.IsInterface)
            {
                throw new ArgumentException("Event must be an interface.", "eventType");
            }

            if (!typeof(IEvent).IsAssignableFrom(eventType))
            {
                throw new ArgumentException("Event must be derrived from IEvent", "eventType");
            }

            var generator = new ProxyGenerator();
            var proxtInterceptor =
                new MultiInterfaceProxy(eventType,
                                        implementationProvider, resolveEventsImmediately);

            var instance = generator.CreateInterfaceProxyWithoutTarget(eventType, proxtInterceptor);

            return instance;
        }


        public void Intercept(IInvocation invocation)
        {

            var returnType = invocation.Method.ReturnType;
            if (returnType.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(returnType.GetGenericTypeDefinition()))
            {
                var list = (dynamic)Activator.CreateInstance(typeof(List<>).MakeGenericType(returnType.GetGenericArguments()[0]));

                foreach (var @event in this.Events)
                {
                    foreach (var elem in (dynamic)invocation.Method.Invoke(@event, invocation.Arguments))
                    {
                        list.Add(elem);
                    }
                }

                invocation.ReturnValue = list;
                return;
            }

            object result = null;

            foreach (var @event in this.Events)
            {
                result = invocation.Method.Invoke(@event, invocation.Arguments);
            }

            invocation.ReturnValue = result;
        }
    }
}