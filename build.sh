#!/usr/bin/env bash

dotnet restore

versions=("0.5.x" "0.6.x")

if [ ! -d "build" ]; then
    mkdir build
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

    if ! dotnet publish ScrollBinding-$version -c Debug -o temp/$version --no-restore -v q
    then
        echo "Failed to build the plugin"
        exit 1
    fi

    echo ""
    echo "Zipping the plugin ($version)"
    echo ""

    mv temp/$version/ScrollBinding.dll build/$version/ScrollBinding.dll
    mv temp/$version/ScrollBinding.pdb build/$version/ScrollBinding.pdb
    mv temp/$version/ScrollBinding.Lib.dll build/$version/ScrollBinding.Lib.dll
    mv temp/$version/ScrollBinding.Lib.pdb build/$version/ScrollBinding.Lib.pdb
    mv temp/$version/ScrollBinding.Native.dll build/$version/ScrollBinding.Native.dll
    mv temp/$version/ScrollBinding.Native.pdb build/$version/ScrollBinding.Native.pdb

    (
        cd ./build/$version

        if ! zip -r ScrollBindings-$version.zip *
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