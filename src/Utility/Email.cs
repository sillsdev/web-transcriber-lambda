using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Mail;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace TranscriberAPI.Utility
{
    public static class Email
    {
        public static void SendEmail(string To, string Subject, string body )
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

                // Try to send the message. Show status in console.
                try
                {
                    Console.WriteLine("Attempting to send email...");
                    client.Send(message);
                    Console.WriteLine("Email sent!");
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
