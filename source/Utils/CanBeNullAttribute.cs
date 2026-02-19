using System.Reflection;
using Vintagestory.API.Common;

namespace PlayerModelLib;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class CanBeNullAttribute : Attribute
{
    public static bool Validate(object instance, ICoreAPI api, string logData)
    {
        IEnumerable<PropertyInfo> properties = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        bool valid = true;
        foreach (PropertyInfo? property in properties)
        {
            if (IsDefined(property, typeof(CanBeNullAttribute)))
            {
                continue;
            }

            object? value = property.GetValue(instance);

            if (value == null)
            {
                valid = false;
                LoggerUtil.Error(api, instance, $"({logData}) Property '{property.Name}' value is null");
            }
        }

        return valid;
    }
}
