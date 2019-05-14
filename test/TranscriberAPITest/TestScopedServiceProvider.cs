using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Moq;
using System;

namespace TranscriberAPI.Tests
{
    public class TestScopedServiceProvider : IScopedServiceProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private Mock<IHttpContextAccessor> _httpContextAccessorMock = new Mock<IHttpContextAccessor>();

        public TestScopedServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IHttpContextAccessor))
            {
                return _httpContextAccessorMock.Object;
            }

            return _serviceProvider.GetService(serviceType);
        }
    }
}
