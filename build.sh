#!/usr/bin/env bash

dotnet restore

versions=("0.5.x" "0.6.x")

if [ ! -d "build" ]; then
    mkdir build
fi

# ------------------------------ Functions ------------------------------ #

build_plugin() {
    echo ""
    echo "Building the plugin ($version)"
    echo ""

    if ! dotnet publish ScrollBinding-$version -c Release -o ./build/$version --no-restore -v q
    then
        echo "Failed to build the plugin"
        exit 1
    fi

    echo ""
    echo "Zipping the plugin ($version)"
    echo ""

    (
        cd ./build/$version

        # .NET doesn't provide a way to a completely minimal publish, so we have to remove all the unnecessary files
        rm ./*.deps.json

        if ! zip -r ScrollBindings-$version.zip ScrollBinding*.dll
        then
            echo "Failed to zip the plugin"
            exit 1
        fi
    )

}

# ------------------------------ Main ------------------------------ #

# Re-create hashes.txt
> "./build/hashes.txt"

for version in "${versions[@]}"; do

    build_plugin $version

    (
        cd ./build/$version

        # Compute checksums
        sha256sum ScrollBindings-$version.zip >> ../hashes.txt
    )

done

echo ""
echo "Plugin build complete"
echo ""