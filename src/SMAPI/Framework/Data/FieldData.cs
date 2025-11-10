using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StardewModdingAPI.Framework.Data;
internal class FieldData
{
    public Type Type { get; init; } = typeof(object);
    public CustomDataFieldBehavior Behavior { get; init; }
    public Delegate? CreationDelegate { get; init; }

    public Delegate GetterDelegate { get; init; } = static () => (object?)null;
    public Delegate SetterDelegate { get; init; } = static (object? val) => {};
}
