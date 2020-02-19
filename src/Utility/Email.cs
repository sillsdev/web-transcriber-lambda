using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using Amazon.Lambda;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Amazon.Lambda.Model;

namespace TranscriberAPI.Utility
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
            public string[] ToAddresses { get; set; }
            public string BodyHtml { get; set; }
            public string Subject { get; set; }
            public string FromEmail { get; set; }
        }
        private static async Task SendEmailLambdaAsync(string To, string Subject, string body)
        {
            string FROM = GetVarOrThrow("SIL_TR_EMAIL_FROM");   // This address must be verified with Amazon SES.
            Console.WriteLine("send email lambda: " + To);
            EmailData payload = new EmailData {
                ToAddresses = To.Split(";"),
                BodyHtml = body,
                Subject = Subject,
                FromEmail = FROM
            };

            InvokeRequest ir = new InvokeRequest
            {
                FunctionName = "SendSESEmail",
                InvocationType = Amazon.Lambda.InvocationType.RequestResponse,
                LogType = "Tail",
                Payload = JsonConvert.SerializeObject(payload)
            };
            AmazonLambdaClient lambdaClient = new AmazonLambdaClient(RegionEndpoint.USEast1);
            try
            {
                InvokeResponse response = await lambdaClient.InvokeAsync(ir);
                Console.WriteLine("Invoke complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine("SendEmailLambda error");
                Console.WriteLine(ex);
                throw ex;
            }

        }
        /*
        private static void SendEmailSMTP(string To, string Subject, string body)
        {
            string FROM = GetVarOrThrow("SIL_TR_EMAIL_FROM");   // This address must be verified with Amazon SES.
            string SMTP_USERNAME = GetVarOrThrow("SIL_TR_EMAIL_SMTP_USERNAME");
            string SMTP_PASSWORD = GetVarOrThrow("SIL_TR_EMAIL_SMTP_PASSWORD");
            string HOST = GetVarOrThrow("SIL_TR_EMAIL_HOST");
            int PORT = 25;

            //These can remain hardcoded?
            String FROMNAME = "SIL Transcriber";


            // (Optional) the name of a configuration set to use for this message.
            // If you comment out this line, you also need to remove or comment out
            // the "X-SES-CONFIGURATION-SET" header below.
            //String CONFIGSET = "ConfigSet";

            // Create and build a new MailMessage object
            MailMessage message = new MailMessage();
            message.IsBodyHtml = true;
            message.From = new MailAddress(FROM, FROMNAME);
            message.To.Add(new MailAddress(To));
            message.Subject = Subject;
            message.Body = body;
            // Comment or delete the next line if you are not using a configuration set
            //message.Headers.Add("X-SES-CONFIGURATION-SET", CONFIGSET);

            using (var client = new System.Net.Mail.SmtpClient(HOST, PORT))
            {
                // Pass SMTP credentials
                client.Credentials =
                    new NetworkCredential(SMTP_USERNAME, SMTP_PASSWORD);

                // Enable SSL encryption
                client.EnableSsl = true;

                try
                {
                    client.Send(message);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }
        */
        private static async Task SendEmailAPIAsync(string To, string Subject, string body)
        {
            string FROM = GetVarOrThrow("SIL_TR_EMAIL_FROM");   // This address must be verified with Amazon SES.
            Console.WriteLine("SendEmailAPIAsync " + To);
            using (var client = new AmazonSimpleEmailServiceClient(RegionEndpoint.USEast1))
            {
                var sendRequest = new SendEmailRequest
                {
                    Source = FROM,
                    Destination = new Destination
                    {
                        ToAddresses = new List<string> { To }
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
                    var response = await client.SendEmailAsync(sendRequest); 
                    Console.WriteLine("The email was sent successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("The email was not sent.");
                    Console.WriteLine("Error message: " + ex.Message);
                    throw ex;

                }
            }
        }
    }
}

