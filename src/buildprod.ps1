dotnet build
dotnet lambda package --configuration release  --output-package bin/release/net10.0/deploy-package_prod.zip
serverless deploy --verbose -s prod