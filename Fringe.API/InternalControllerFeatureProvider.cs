using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Fringe.API;

/// <summary>Extends controller discovery to include internal controller types.</summary>
internal sealed class InternalControllerFeatureProvider : ControllerFeatureProvider
{
    /// <inheritdoc/>
    protected override bool IsController(TypeInfo typeInfo)
    {
        return typeInfo.IsClass
            && !typeInfo.IsAbstract
            && !typeInfo.ContainsGenericParameters
            && !typeInfo.IsDefined(typeof(NonControllerAttribute), inherit: true)
            && (typeof(ControllerBase).IsAssignableFrom(typeInfo)
                || typeInfo.IsDefined(typeof(ControllerAttribute), inherit: true));
    }
}
