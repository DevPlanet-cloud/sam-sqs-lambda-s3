using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Text;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SAM_SQS_Lambda_S3;

public class Function
{
    private string _sourceSQSURL = Environment.GetEnvironmentVariable("SourceSQSURL");
    private string _bucketName = Environment.GetEnvironmentVariable("BucketName");

    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {

    }


    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
    /// to respond to SQS messages.
    /// </summary>
    /// <param name="evnt">The event for the Lambda function handler to process.</param>
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        foreach(var message in evnt.Records)
        {
            await ProcessMessageAsync(message, context);
        }
    }

    private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
    {
        context.Logger.LogInformation($"Processed message {message.Body}");

        // Get ReceiptHandle to delete the message from SQS after processing
        string receiptHandle = message.ReceiptHandle;

        // Save message body to S3 bucket as a file
        await SaveMessageToS3Bucket(message.Body);

        // Finally delete message from the queue
        await DeleteMessageFromSQS(receiptHandle);
    }

    private async Task SaveMessageToS3Bucket(string messageBody)
    {
        Console.WriteLine($"Going to save '{messageBody}' to S3.");
        byte[] byteArray = Encoding.ASCII.GetBytes(messageBody);
        MemoryStream fileStream = new MemoryStream(byteArray);

        PutObjectRequest request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key =  $"SamDemoFile-{DateTime.Now.ToString("ddMMyyyy-HHmmss")}"
        };

        using (var f = fileStream)
        {
            request.InputStream = fileStream;
            AmazonS3Client amazonS3Client = new AmazonS3Client();
            await amazonS3Client.PutObjectAsync(request);
        }

        Console.WriteLine("File created successfully.");
    }

    private async Task DeleteMessageFromSQS(string receiptHandle)
    {
        var deleteMessageRequest = new DeleteMessageRequest
        {
            QueueUrl = _sourceSQSURL,
            ReceiptHandle = receiptHandle
        };

        AmazonSQSClient sqsClient = new AmazonSQSClient();
        await sqsClient.DeleteMessageAsync(deleteMessageRequest);

        Console.WriteLine("Message has beend deleted from the queue successfully.");
    }
}