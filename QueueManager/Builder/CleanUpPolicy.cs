namespace QueueManager
{
    /// <summary>
    /// Class used to configure the scheduled cleaning of queues by a <see cref="QueuingManager{I, T}"/> instance.
    /// </summary>
    public class CleanUpPolicy
    {
        /// <summary>
        /// Value representing the frequency, in days, in which the operation will run.
        /// </summary>
        public double Interval { get; }
        /// <summary>
        /// The hour component of the scheduled time for the operation to start.
        /// </summary>
        public int Hour { get; }
        /// <summary>
        /// The minutes component of the scheduled time for the operation to start.
        /// </summary>
        public int Minutes { get; }

        /// <summary>
        /// Initiates a new instance of <see cref="CleanUpPolicy"/>.
        /// </summary>
        /// <param name="interval">The frequency, in days, in which the operation will run.
        /// Ex.: 1 = daily, 0.5 = Twice a day, etc.</param>
        /// <param name="hour">The hour component of a scheduled time for the operation to start.</param>
        /// <param name="minutes">The minute component of a scheduled time for the operation to start.</param>
        public CleanUpPolicy(double interval, Hour hour, Minute minutes)
        {
            Interval = interval;
            Hour = (int)hour;
            Minutes = (int)minutes;
        }
    }

    /// <summary>
    /// Enum representation of 24-hours for ease of use.
    /// </summary>
    public enum Hour
    {
        H00,
        H01,
        H02,
        H03,
        H04,
        H05,
        H06,
        H07,
        H08,
        H09,
        H10,
        H11,
        H12,
        H13,
        H14,
        H15,
        H16,
        H17,
        H18,
        H19,
        H20,
        H21,
        H22,
        H23
    }

    /// <summary>
    /// Enum representation of full and half hour for ease of use.
    /// </summary>
    public enum Minute
    {
        M00,
        M30 = 30
    }
}
