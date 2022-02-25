dotnet build
dotnet lambda package --configuration release --framework netcoreapp2.1 --output-package bin/release/netcoreapp2.1/deploy-package_qaDBG.zip
serverless deploy -v -s qaDBG