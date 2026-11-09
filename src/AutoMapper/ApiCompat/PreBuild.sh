#!/bin/bash
# Downloads the official AutoMapper package the public API is compared with:
# the previous major for an x.0.0 version, otherwise the current major.
set -e
version=$1
echo $version
IFS=. read -r -a versionNumbers <<< "$version"
if [[ "${versionNumbers[1]}" == "0" && "${versionNumbers[2]}" == "0" ]]
then
    oldVersion=$((${versionNumbers[0]} - 1))
else
    oldVersion=${versionNumbers[0]}
fi
oldVersion="$oldVersion.0.0"
echo $oldVersion
rm -rf ../LastMajorVersionBinary
curl --fail --silent --show-error --location https://globalcdn.nuget.org/packages/automapper.$oldVersion.nupkg --create-dirs -o ../LastMajorVersionBinary/automapper.$oldVersion.nupkg
# The target framework folder differs between majors (netstandard2.1, net6.0, net8.0, ...).
unzip -o -j ../LastMajorVersionBinary/automapper.$oldVersion.nupkg 'lib/net*/AutoMapper.dll' -d ../LastMajorVersionBinary
