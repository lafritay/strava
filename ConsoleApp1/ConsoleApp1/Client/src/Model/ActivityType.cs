using System.Runtime.Serialization;

namespace IO.Swagger.Model
{
    /// <summary>
    /// An enumeration of the types an activity may have.
    /// </summary>
    [DataContract]
    public enum ActivityType
    {
        Run,
        Ride,
        Walk,
        Hike,
        Wheelchair,
        Workout,
        NordicSki,
Kayaking,
Windsurf
    }
}
