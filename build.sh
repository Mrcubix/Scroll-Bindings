#!/usr/bin/env bash

dotnet restore

versions=("0.5.x" "0.6.x")

if [ ! -d "build" ]; then
    mkdir build
else
    rm -rf build/*
fi

# ------------------------------ Functions ------------------------------ #

create_plugin_structure() {
    (
        if [ ! -d "build" ]; then
            mkdir build
        fi

        cd build

        for version in "${versions[@]}"; do
            if [ ! -d "./$version" ]; then
                mkdir $version
            fi
        done
    )
}

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

        if ! zip -r ScrollBindings-$version.zip *.dll
        then
            echo "Failed to zip the plugin"
            exit 1
        fi
    )

}

# ------------------------------ Main ------------------------------ #

# Re-create hashes.txt
> "./build/hashes.txt"

create_plugin_structure

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