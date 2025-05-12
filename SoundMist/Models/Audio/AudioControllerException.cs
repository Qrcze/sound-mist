using System;

namespace SoundMist.Models.Audio
{
    [Serializable]
    internal class AudioControllerException : Exception
    {
        public AudioControllerException()
        {
        }

        public AudioControllerException(string? message) : base(message)
        {
        }

        public AudioControllerException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}