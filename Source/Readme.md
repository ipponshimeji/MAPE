# How to build

## Build Environment

Visual Studio 2015

## Instructions to build

1. Open Mape.sln with Visual Studio 2015.
1. Build it.
1. Output files are built into `(Solution Folder)\__Bin\(Config)\Windows`,
where `(Config)` is the current configuration: `Debug`, `Release` or `Release_Signed`.
1. In `Release` configuration, the packaged file is built in `(Solution Folder)\__Package\Release` 
1. In `Release_Signed` configuration, a batch file to create the packaged file is built as `(Solution Folder)\__Obj\Release_Singed\_Packaging\Windows\SignAndZip.bat`.
Executing it with key file, the packaged file is generated in `(Solution Folder)\__Package\Release_Signed`.
See "To give strong name to MAPE assemblies" below for details.


### To give strong name to MAPE assemblies

You can give strong name to MAPE assemblies building the solution with `Release_Signed` configuration.
But it uses delay signing.
You must prepare your key and sign assemblies with it by following steps:

1. Prepare your key pair file. Suppose that its name is `MyKeyPair.snk`. For example, run `sn -k MyKeyPair.snk`.
1. Extract its public key to a file named `publickey.snk`. That is, run `sn -p MyKeyPair.snk publickey.snk`.
1. Replace the `publickey.snk` in the solution (MAPE.sln) with your public key. The original publickey in the solution is mine.
1. Keep your key pair (`MyKeyPair.snk`) secret.
1. Build the solution with `Release_Signeed` configuration. Then a batch file `(Solution Folder)\__Obj\Release_Singed\_Packaging\Windows\SignAndZip.bat` is built.
1. Execute the batch file specifying your key pair. For example `SignAndZip.bat MyKeyPair.snk`. Then the built MAPE assemblies are signed, and zipped into `(Solution Folder)\__Package\Release_Signed` folder.
