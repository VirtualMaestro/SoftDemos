using System;

namespace Client.Adapters.Shared
{
    /// <summary>
    /// Marks a serialized field that may legitimately be left unassigned, so the guard in
    /// <c>OnValidate</c> does not have to claim it is required.
    /// </summary>
    /// <remarks>
    /// A serialized reference is required by default and <c>DEU0121</c> reports one the guard
    /// forgot — a class that validates and then dies on the one field nobody wired. This attribute
    /// is the other answer: the field is optional BY DESIGN and the code handles its absence. The
    /// stage cameras are the case it was written for: a screen that carries none falls back to
    /// <c>Camera.main</c>, which is what the systems did before the camera moved onto the screen.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class OptionalAttribute : Attribute
    {
    }
}
