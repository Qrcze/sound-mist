using SoundMist.Models;

namespace SCPlayerTests
{
    internal class DummyLogger : ILogger
    {
        public string LastMessage { get; private set; } = string.Empty;

        public void Info(string message)
        {
            LastMessage = message;
        }

        public void Warn(string message)
        {
            LastMessage = message;
        }

        public void Error(string message)
        {
            LastMessage = message;
            Assert.Fail(message);
        }

        public void Fatal(string message)
        {
            LastMessage = message;
            Assert.Fail(message);
        }
    }
}