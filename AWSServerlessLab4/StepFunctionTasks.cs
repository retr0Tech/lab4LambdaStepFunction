using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.DynamoDBEvents;
using Amazon.Lambda.S3Events;


using Amazon.Rekognition;
using Amazon.Rekognition.Model;

using Amazon.S3;
using Amazon.S3.Model;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AWSServerlessLab4;

public class StepFunctionTasks
{
    public const float DEFAULT_MIN_CONFIDENCE = 90f;
    public const string MIN_CONFIDENCE_ENVIRONMENT_VARIABLE_NAME = "MinConfidence";
    IAmazonS3 S3Client { get; }
    IAmazonRekognition RekognitionClient { get; }
    float MinConfidence { get; set; } = DEFAULT_MIN_CONFIDENCE;

    HashSet<string> SupportedImageTypes { get; } = new HashSet<string> { ".png", ".jpg", ".jpeg" };
    /// <summary>
    /// Default constructor that Lambda will invoke.
    /// </summary>
    public StepFunctionTasks()
    {
        this.S3Client = new AmazonS3Client();
        this.RekognitionClient = new AmazonRekognitionClient();

        var environmentMinConfidence = System.Environment.GetEnvironmentVariable(MIN_CONFIDENCE_ENVIRONMENT_VARIABLE_NAME);
        if (!string.IsNullOrWhiteSpace(environmentMinConfidence))
        {
            float value;
            if (float.TryParse(environmentMinConfidence, out value))
            {
                this.MinConfidence = value;
                Console.WriteLine($"Setting minimum confidence to {this.MinConfidence}");
            }
            else
            {
                Console.WriteLine($"Failed to parse value {environmentMinConfidence} for minimum confidence. Reverting back to default of {this.MinConfidence}");
            }
        }
        else
        {
            Console.WriteLine($"Using default minimum confidence of {this.MinConfidence}");
        }
    }
    public async Task FunctionHandler(S3Event input, ILambdaContext context)
    {
        foreach (var record in input.Records)
        {
            if (!SupportedImageTypes.Contains(Path.GetExtension(record.S3.Object.Key)))
            {
                Console.WriteLine($"Object {record.S3.Bucket.Name}:{record.S3.Object.Key} is not a supported image type");
                continue;
            }

            Console.WriteLine($"Looking for labels in image {record.S3.Bucket.Name}:{record.S3.Object.Key}");
            var detectResponses = await this.RekognitionClient.DetectLabelsAsync(new DetectLabelsRequest
            {
                MinConfidence = MinConfidence,
                Image = new Image
                {
                    S3Object = new Amazon.Rekognition.Model.S3Object
                    {
                        Bucket = record.S3.Bucket.Name,
                        Name = record.S3.Object.Key
                    }
                }
            });

            var tags = new List<Tag>();
            foreach (var label in detectResponses.Labels)
            {
                if (tags.Count < 10)
                {
                    Console.WriteLine($"\tFound Label {label.Name} with confidence {label.Confidence}");
                    tags.Add(new Tag { Key = label.Name, Value = label.Confidence.ToString() });
                }
                else
                {
                    Console.WriteLine($"\tSkipped label {label.Name} with confidence {label.Confidence} because the maximum number of tags has been reached");
                }
            }

            await this.S3Client.PutObjectTaggingAsync(new PutObjectTaggingRequest
            {
                BucketName = record.S3.Bucket.Name,
                Key = record.S3.Object.Key,
                Tagging = new Tagging
                {
                    TagSet = tags
                }
            });
        }
        return;
    }
    public void FunctionHandlerDynamo(DynamoDBEvent dynamoEvent, ILambdaContext context)
    {
        context.Logger.LogLine($"Beginning to process {dynamoEvent.Records.Count} records...");

        foreach (var record in dynamoEvent.Records)
        {
            context.Logger.LogLine($"Event ID: {record.EventID}");
            context.Logger.LogLine($"Event Name: {record.EventName}");

            // TODO: Add business logic processing the record.Dynamodb object.
        }

        context.Logger.LogLine("Stream processing complete.");
    }
    public async Task<string> FunctionHandlerS3(S3Event evnt, ILambdaContext context)
    {
        var s3Event = evnt.Records?[0].S3;
        if (s3Event == null)
        {
            return null;
        }

        try
        {
            var response = await this.S3Client.GetObjectMetadataAsync(s3Event.Bucket.Name, s3Event.Object.Key);
            return response.Headers.ContentType;
        }
        catch (Exception e)
        {
            context.Logger.LogLine($"Error getting object {s3Event.Object.Key} from bucket {s3Event.Bucket.Name}. Make sure they exist and your bucket is in the same region as this function.");
            context.Logger.LogLine(e.Message);
            context.Logger.LogLine(e.StackTrace);
            throw;
        }
    }
    public State Greeting(State state, ILambdaContext context)
    {
        state.Message = "Hello";

        if(!string.IsNullOrEmpty(state.Name))
        {
            state.Message += " " + state.Name;
        }

        // Tell Step Function to wait 5 seconds before calling 
        state.WaitInSeconds = 5;

        return state;
    }

    public State Salutations(State state, ILambdaContext context)
    {
        state.Message += ", Goodbye";

        if (!string.IsNullOrEmpty(state.Name))
        {
            state.Message += " " + state.Name;
        }

        return state;
    }
    public void CreateThumbnailAndInsertToBucket()
    {

    }
}