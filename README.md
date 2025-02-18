# Order-ORM

A C# application for querying DICOM worklists and generating ORM messages.

## License

This software is released under the **Medical Software Academic and Restricted Use License**:
- **Academic Users**: Free to download, use, and modify for non-commercial purposes with attribution.
- **Commercial Use**: Prohibited without written permission from [Your Company Name].
- **Competitors**: Prohibited from use.
- **Clients**: Prohibited from use by clients of licensees.

See the [LICENSE](LICENSE.md) file for full terms.

## Requirements

- Windows 10 with .NET Framework 4.7.2 (pre-installed with Windows updates)
- Required NuGet packages:
  - fo-dicom (4.0.8)
  - System.Data.SQLite (1.0.118)

## Installation

1. Clone the repository
2. Install dependencies:
```
dotnet restore
```
3. Configure `App.config` with your server details
4. Build the project:
```
dotnet build
```

## Usage
```
Order-ORM.exe <spsStartDate> <modality> <stationName>
```

Example:

```
Order-ORM.exe 20250218 CT CTSTATION1
```


## Configuration

Edit `App.config` with your DICOM server details:
```xml
<appSettings>
  <add key="WorklistHost" value="worklist.example.com" />
  <add key="WorklistPort" value="104" />
  <add key="WorklistCallingAE" value="WORKLISTCLIENT" />
  <add key="WorklistCalledAE" value="WORKLISTSRV" />
  <add key="DestinationHost" value="destination.example.com" />
  <add key="DestinationPort" value="104" />
  <add key="DestinationCallingAE" value="ORMCLIENT" />
  <add key="DestinationCalledAE" value="DESTINATION" />
</appSettings>
```

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Submit a pull request
4. Include attribution to the original work

## Attribution

When using or modifying this software, please include:
"Based on Order-ORM by Flux Inc (https://fluxinc.co), used under the Medical Software Academic and Restricted Use License"

## Contact

For commercial licensing: [sales@fluxinc.co (mailto:sales@fluxinc.co)]
