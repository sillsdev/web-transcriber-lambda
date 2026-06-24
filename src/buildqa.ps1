dotnet build
dotnet lambda package --configuration release --output-package bin/release/net10.0/deploy-package_qa.zip
serverless deploy --verbose -s qa
