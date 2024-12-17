using Amazon;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Newtonsoft.Json;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Utility
{
    public static class Email
    {
        public static async Task SendEmailAsync(string To, string Subject, string body)
        {
            bool isLambda = GetVarOrDefault("LAMBDA_TASK_ROOT", "") != "";
            Console.WriteLine("SendEmailAsync " + To);
            Console.WriteLine(isLambda);

            if (isLambda)
            {
                await SendEmailLambdaAsync(To, Subject, body);
            }
            else
            {
                await SendEmailAPIAsync(To, Subject, body);
            }
        }
        public class EmailData
        {
            public EmailData() : base()
            {
                ToAddresses = [];
                BodyHtml = "";
                Subject = "";
                FromEmail = "";
            }
            public string[] ToAddresses { get; set; }
            public string BodyHtml { get; set; }
            public string Subject { get; set; }
            public string FromEmail { get; set; }
        }
        private static async Task SendEmailLambdaAsync(string To, string Subject, string body)
        {
            string FROM = GetVarOrThrow("SIL_TR_EMAIL_FROM");   // This address must be verified with Amazon SES.
            Console.WriteLine("send email lambda: " + To);
            EmailData payload = new()
            {
                ToAddresses = To.Split(";"),
                BodyHtml = body,
                Subject = Subject,
                FromEmail = FROM
            };

            InvokeRequest ir = new ()
            {
                FunctionName = "SendSESEmail",
                InvocationType = Amazon.Lambda.InvocationType.RequestResponse,
                LogType = "Tail",
                Payload = JsonConvert.SerializeObject(payload)
            };
            AmazonLambdaClient lambdaClient = new (RegionEndpoint.USEast1);
            try
            {
                InvokeResponse response = await lambdaClient.InvokeAsync(ir);
                Console.WriteLine("Invoke complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine("SendEmailLambda error");
                Console.WriteLine(ex);
                throw;
            }

        }

        private static async Task SendEmailAPIAsync(string To, string Subject, string body)
        {
            string FROM = GetVarOrThrow("SIL_TR_EMAIL_FROM");   // This address must be verified with Amazon SES.
            Console.WriteLine("SendEmailAPIAsync " + To);
            using AmazonSimpleEmailServiceClient? client = new(RegionEndpoint.USEast1);
            SendEmailRequest? sendRequest = new()
            {
                Source = FROM,
                Destination = new Destination
                {
                    ToAddresses = [To]
                },
                Message = new Message
                {
                    Subject = new Content(Subject),
                    Body = new Body
                    {
                        Html = new Content
                        {
                            Charset = "UTF-8",
                            Data = body
                        },/*
                            Text = new Content
                            {
                                Charset = "UTF-8",
                                Data = textBody
                            }*/
                    }
                },
                // If you are not using a configuration set, comment
                // or remove the following line 
                //ConfigurationSetName = configSet
            };
            try
            {
                SendEmailResponse? response = await client.SendEmailAsync(sendRequest);
                Console.WriteLine("The email was sent successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("The email was not sent.");
                Console.WriteLine("Error message: " + ex.Message);
                throw;
            }
        }
    }
}

