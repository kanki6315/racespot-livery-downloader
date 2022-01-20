namespace WpfApp1
{
    public interface IMySettings
    {
        string UserPath { get; set; }
        string AWSAccessKey { get; }
        string AWSSecretKey { get; }
        string BucketName { get; }
    }
}