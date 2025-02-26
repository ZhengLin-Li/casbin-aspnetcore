﻿using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Casbin.Model;
using Casbin.Adapter.File;
using Casbin.Persist;

namespace Casbin.AspNetCore.Authorization
{
    public class DefaultEnforcerProvider : IEnforcerProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptions<CasbinAuthorizationOptions> _options;
        private readonly ICasbinModelProvider _modelProvider;
        private IEnforcer? _enforcer;

        public DefaultEnforcerProvider(IServiceProvider serviceProvider, IOptions<CasbinAuthorizationOptions> options,
            ICasbinModelProvider modelProvider)
        {
            _serviceProvider = serviceProvider;
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _modelProvider = modelProvider ?? throw new ArgumentNullException(nameof(modelProvider));
        }

        public virtual IEnforcer? GetEnforcer()
        {
            if (_enforcer is not null)
            {
                return _enforcer;
            }

            if (_options.Value.DefaultEnforcerFactory is not null)
            {
                using IServiceScope scope = _serviceProvider.CreateScope();
                _enforcer ??= _options.Value.DefaultEnforcerFactory(scope.ServiceProvider, _modelProvider.GetModel());
                return _enforcer;
            }

            IModel? model = _modelProvider.GetModel();
            if (model is null)
            {
                throw new ArgumentException($"GetModel method of {nameof(ICasbinModelProvider)} can not return null when {nameof(_options.Value.DefaultEnforcerFactory)} option is empty");
            }

            if (_options.Value.DefaultEnforcerFactory is not null)
            {
                using IServiceScope scope = _serviceProvider.CreateScope();
                _enforcer ??= _options.Value.DefaultEnforcerFactory(scope.ServiceProvider, _modelProvider.GetModel());
                return _enforcer;
            }

            IAdapter? adapter = _serviceProvider.GetService<IAdapter>();
            if (adapter != null)
            {
                _enforcer ??= SyncedEnforcer.Create(model, adapter, true);
                return _enforcer;
            }

            string? policyPath = _options.Value.DefaultPolicyPath;
            if (policyPath is not null)
            {
                if (File.Exists(policyPath) is false)
                {
                    throw new FileNotFoundException("Can not find the policy file path.", policyPath);
                }
                _enforcer ??= SyncedEnforcer.Create(model, new FileAdapter(policyPath), true);
                return _enforcer;
            }

            _enforcer ??= SyncedEnforcer.Create(model);
            return _enforcer;
        }
    }
}
