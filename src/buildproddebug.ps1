﻿dotnet build
dotnet lambda package --configuration release --framework netcoreapp2.1 --output-package bin/release/netcoreapp2.1/deploy-package_prod.zip
serverless deploy -v -s prod --debug DBG