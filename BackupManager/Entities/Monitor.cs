namespace BackupManager.Entities
{
    public class Monitor
    {
        public string Url;
        public int Timeout;
        public string ProcessToKill;
        public string ApplicationToStart;
        public string ApplicationToStartArguments;
        public string Name;
        public int Port;
        public string ServiceToRestart;
    }
}
