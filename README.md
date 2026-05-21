# RSVidReader

C# library for reading Serbian vehicle registration cards (Saobraćajna dozvola) via smart card readers.  
Reads document info, full vehicle data, and owner/user details from the chip.

---

## Requirements

| | |
|---|---|
| **OS** | Windows 10 / Windows 11 |
| **Framework** | .NET Framework 4.8 (Windows Desktop) |
| **DLL** | eVehicleRegistrationAPI.dll (MUP RS SDK) |
| **Hardware** | PC/SC compatible smart card reader |

---

## eVehicleRegistrationAPI.dll Setup

### 1. Get the DLL

`eVehicleRegistrationAPI.dll` is distributed as part of the official MUP RS eVehicle Registration SDK.

Download the correct version based on your project platform:

- `eVehicleRegistrationAPI 64-bit` → for **x64** projects
- `eVehicleRegistrationAPI 32-bit` → for **x86** projects

> **Important:** The project Platform Target must exactly match the DLL version.  
> `Any CPU` will not work — must be explicitly set to `x64` or `x86`.  
> Set it at: Project Properties → Build → Platform target

### 2. Add the DLL to your project

1. Right-click the project in Solution Explorer → **Add → Existing Item**
2. Browse to and select `eVehicleRegistrationAPI.dll`
3. Click **Add**

### 3. Set Copy to Output Directory

1. Click on `eVehicleRegistrationAPI.dll` in Solution Explorer
2. In the **Properties** panel find **Copy to Output Directory**
3. Set it to **Copy always**

> This ensures the DLL is copied next to the `.exe` on every build.  
> No COM registration (`regsvr32`) is required — the library uses direct P/Invoke.

---

## Installation

Copy `RsVidReader.cs` (`RSVidReaderLib` namespace) into your project.

---

## Usage

```csharp
using RSVidReaderLib;

// Check reader and card status
if (!RsVidReader.HasReader())
{
    MessageBox.Show("Please connect a card reader to a USB port.", "Reader Not Found",
        MessageBoxButtons.OK, MessageBoxIcon.Warning);
    return;
}

if (!RsVidReader.HasCard())
{
    MessageBox.Show("Please insert the vehicle registration card.", "No Card Detected",
        MessageBoxButtons.OK, MessageBoxIcon.Information);
    return;
}

// Read the card (async – does not block the UI)
try
{
    RsVidData card = await RsVidReader.ReadAsync();

    txtRegistracija.Text   = card.RegistrationNumber;
    txtBrojSasije.Text     = card.VehicleIDNumber;
    txtBrojMotora.Text     = card.EngineIDNumber;
    txtMarka.Text          = card.CommercialDescription;
    txtGodProizvodnje.Text = card.YearOfProduction;
    txtKubikaza.Text       = card.EngineCapacity;
    txtKs.Text             = card.MaximumNetPower;
    txtSopstvenaMasa.Text  = card.VehicleMass;
    txtUkupnaMasa.Text     = card.MaximumPermissibleLadenMass;
    txtNosivost.Text       = card.VehicleLoad;
    txtBrSedista.Text      = card.NumberOfSeats;
    txtTipVozila.Text      = card.VehicleCategory;
    txtBrojLicence.Text    = card.UnambiguousNumber;
}
catch (CardNotFoundException ex)
{
    MessageBox.Show(ex.Message, "Card Not Found",
        MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
catch (ReaderNotFoundException ex)
{
    MessageBox.Show(ex.Message, "Reader Not Available",
        MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
catch (CardReadException ex)
{
    MessageBox.Show(ex.Message, "Read Error",
        MessageBoxButtons.OK, MessageBoxIcon.Error);
}
```

---

## RsVidData — Available Fields

### Document

| Field | Description |
|---|---|
| `UnambiguousNumber` | Unique vehicle registration number |
| `SerialNumber` | Serial number of the registration card |
| `IssuingDate` | Issue date (raw) |
| `IssuingDateFormatted` | Issue date (DD.MM.YYYY.) |
| `ExpiryDate` | Expiry date (raw) |
| `ExpiryDateFormatted` | Expiry date (DD.MM.YYYY.) |
| `StateIssuing` | Issuing state |
| `AuthorityIssuing` | Issuing authority |
| `CompetentAuthority` | Competent authority |

### Vehicle

| Field | Description |
|---|---|
| `RegistrationNumber` | License plate number |
| `VehicleIDNumber` | Chassis / VIN number |
| `EngineIDNumber` | Engine number |
| `VehicleMake` | Make |
| `VehicleType` | Type |
| `CommercialDescription` | Commercial description (model) |
| `VehicleCategory` | Vehicle category |
| `ColourOfVehicle` | Colour |
| `YearOfProduction` | Year of production |
| `DateOfFirstRegistration` | Date of first registration (raw) |
| `FirstRegistrationFormatted` | Date of first registration (DD.MM.YYYY.) |
| `EngineCapacity` | Engine capacity (cc) |
| `MaximumNetPower` | Maximum net power (kW) |
| `TypeOfFuel` | Type of fuel |
| `VehicleMass` | Vehicle mass (kg) |
| `MaximumPermissibleLadenMass` | Maximum permissible laden mass (kg) |
| `VehicleLoad` | Load capacity (kg) |
| `NumberOfSeats` | Number of seats |
| `NumberOfStandingPlaces` | Number of standing places |
| `NumberOfAxles` | Number of axles |
| `TypeApprovalNumber` | Type approval number |
| `PowerWeightRatio` | Power/weight ratio (kg/kW) |
| `RestrictionToChangeOwner` | Restriction to change owner |

### Owner

| Field | Description |
|---|---|
| `OwnersPersonalNo` | Owner's JMBG or company ID |
| `OwnersSurname` | Owner's surname or business name |
| `OwnersName` | Owner's first name |
| `OwnersAddress` | Owner's address |
| `OwnerFullName` | Owner's first name + surname |

### User (if different from owner)

| Field | Description |
|---|---|
| `UsersPersonalNo` | User's JMBG or company ID |
| `UsersSurname` | User's surname or business name |
| `UsersName` | User's first name |
| `UsersAddress` | User's address |
| `UserFullName` | User's first name + surname |

---

## API Reference

```csharp
// Static – no instance required
RsVidReader.HasReader()      // bool – checks if a reader is connected
RsVidReader.HasCard()        // bool – checks if a card is inserted
RsVidReader.ReadAsync()      // Task<RsVidData> – reads all card data

// Instance usage
using (var reader = new RsVidReader())
{
    RsVidData card = reader.Read();
}
```

---

## Error Codes

| Code | Description |
|---|---|
| `0x8010000C` | No smart card in reader |
| `0x80100009` | Unknown or unavailable reader |
| `0x8010002E` | No readers available |
| `0x8010001C` | Card unsupported (not a vehicle registration card) |
| `11` | Bad data format |
| `12` | Invalid access (ProcessNewCard not called) |
| `13` | Invalid data on chip |

---

## Notes

- All text fields are automatically transliterated from Cyrillic to Latin script.
- `ReadAsync` runs on a dedicated STA thread — safe for WinForms and WPF.
- `HasReader` uses the SDK's own `GetReaderName` function for accurate detection.
- `HasCard` uses the Windows WinSCard API — non-blocking.
- The library uses the C API (`eVehicleRegistrationAPI.dll`) via P/Invoke — no COM registration required.
- If the DLL returns wrong data, ensure project Platform Target matches the DLL bitness (x64 or x86).

---

## License

MIT
