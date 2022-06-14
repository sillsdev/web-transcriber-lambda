using Amazon.Lambda.AspNetCoreServer;

namespace SIL.Transcriber
{
    public class LambdaEntryPoint : APIGatewayProxyFunction
    {
        protected override void Init(IWebHostBuilder builder)
        {
            RegisterResponseContentEncodingForContentType(
                "audio/mp3",
                ResponseContentEncoding.Base64
            );
            RegisterResponseContentEncodingForContentType(
                "audio/mpeg",
                ResponseContentEncoding.Base64
            );
            RegisterResponseContentEncodingForContentType(
                "audio/mp4",
                ResponseContentEncoding.Base64
            );
            RegisterResponseContentEncodingForContentType(
                "audio/vnd.wav",
                ResponseContentEncoding.Base64
            );
            //haha you wish...this doesn't work...
            RegisterResponseContentEncodingForContentType(
                "audio/*",
                ResponseContentEncoding.Base64
            );

            builder.UseStartup<Startup>();
        }
    }
}
