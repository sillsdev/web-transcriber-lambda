dotnet build 
dotnet lambda package --configuration release --framework netcoreapp3.1 --output-package bin/release/netcoreapp3.1/deploy-package_dev.zip
serverless deploy -v -s dev