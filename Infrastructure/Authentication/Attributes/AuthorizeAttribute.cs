namespace HoTeach_Functions_API.Infrastructure.Authentication.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class AuthorizeAttribute : Attribute
    {
        public string Scope { get; }

        // Constructor with scope parameter
        public AuthorizeAttribute(string scope)
        {
            Scope = scope;
        }

        // Parameterless constructor for general authorization without specific scope
        public AuthorizeAttribute()
        {
            Scope = string.Empty;
        }
    }
}
