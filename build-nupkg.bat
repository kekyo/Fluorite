@echo off

rem Fluorite - Simplest and fully-customizable RPC standalone infrastructure.
rem Copyright (c) 2021 Kouji Matsui (@kozy_kekyo, @kekyo2)
rem 
rem Licensed under the Apache License, Version 2.0 (the "License");
rem you may not use this file except in compliance with the License.
rem You may obtain a copy of the License at
rem 
rem http://www.apache.org/licenses/LICENSE-2.0
rem 
rem Unless required by applicable law or agreed to in writing, software
rem distributed under the License is distributed on an "AS IS" BASIS,
rem WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
rem See the License for the specific language governing permissions and
rem limitations under the License.

echo.
echo "==========================================================="
echo "Build Fluorite"
echo.

rem git clean -xfd

if not exist artifacts (
    mkdir artifacts
) else (
    del /q /f /s artifacts\*.*
)

dotnet restore
dotnet build -c Release -p:Platform="Any CPU" Fluorite.sln

dotnet pack -p:Configuration=Release -p:Platform=AnyCPU -o artifacts Fluorite.Core\Fluorite.Core.csproj
dotnet pack -p:Configuration=Release -p:Platform=AnyCPU -o artifacts Fluorite.Serializer\Fluorite.Serializer.csproj
dotnet pack -p:Configuration=Release -p:Platform=AnyCPU -o artifacts Fluorite.Transport\Fluorite.Transport.csproj
dotnet pack -p:Configuration=Release -p:Platform=AnyCPU -o artifacts Fluorite.Build\Fluorite.Build.csproj

dotnet pack -p:Configuration=Release -p:Platform=AnyCPU -o artifacts Fluorite\Fluorite.csproj
dotnet pack -p:Configuration=Release -p:Platform=AnyCPU -o artifacts Fluorite.Dynamic\Fluorite.Dynamic.csproj
