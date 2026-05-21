using System;
using System.Threading.Tasks;
using RSVidReaderLib;

namespace RsVidConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            if (!RsVidReader.HasReader())
            {
                Console.WriteLine("[ERROR] No card reader detected. Please connect a USB card reader.");
                Exit(); return;
            }

            if (!RsVidReader.HasCard())
            {
                Console.WriteLine("[ERROR] No card inserted. Please insert the vehicle registration card.");
                Exit(); return;
            }

            Console.WriteLine("Reading vehicle registration card...");

            try
            {
                RsVidData card = await RsVidReader.ReadAsync();

                Console.WriteLine();
                Console.WriteLine("══════════════════════════════════");
                Console.WriteLine("  DOCUMENT");
                Console.WriteLine("══════════════════════════════════");
                Console.WriteLine($"Unique number    : {card.UnambiguousNumber}");
                Console.WriteLine($"Serial number    : {card.SerialNumber}");
                Console.WriteLine($"Issuing date     : {card.IssuingDateFormatted}");
                Console.WriteLine($"Expiry date      : {card.ExpiryDateFormatted}");
                Console.WriteLine($"State issuing    : {card.StateIssuing}");
                Console.WriteLine($"Authority        : {card.AuthorityIssuing}");
                Console.WriteLine($"Competent auth.  : {card.CompetentAuthority}");

                Console.WriteLine();
                Console.WriteLine("══════════════════════════════════");
                Console.WriteLine("  VEHICLE");
                Console.WriteLine("══════════════════════════════════");
                Console.WriteLine($"Registration     : {card.RegistrationNumber}");
                Console.WriteLine($"Chassis (VIN)    : {card.VehicleIDNumber}");
                Console.WriteLine($"Engine number    : {card.EngineIDNumber}");
                Console.WriteLine($"Make             : {card.VehicleMake}");
                Console.WriteLine($"Type             : {card.VehicleType}");
                Console.WriteLine($"Model            : {card.CommercialDescription}");
                Console.WriteLine($"Category         : {card.VehicleCategory}");
                Console.WriteLine($"Colour           : {card.ColourOfVehicle}");
                Console.WriteLine($"Year             : {card.YearOfProduction}");
                Console.WriteLine($"First reg.       : {card.FirstRegistrationFormatted}");
                Console.WriteLine($"Engine capacity  : {card.EngineCapacity} cc");
                Console.WriteLine($"Max net power    : {card.MaximumNetPower} kW");
                Console.WriteLine($"Fuel type        : {card.TypeOfFuel}");
                Console.WriteLine($"Own mass         : {card.VehicleMass} kg");
                Console.WriteLine($"Max laden mass   : {card.MaximumPermissibleLadenMass} kg");
                Console.WriteLine($"Load capacity    : {card.VehicleLoad} kg");
                Console.WriteLine($"Seats            : {card.NumberOfSeats}");
                Console.WriteLine($"Standing places  : {card.NumberOfStandingPlaces}");
                Console.WriteLine($"Axles            : {card.NumberOfAxles}");
                Console.WriteLine($"Type approval    : {card.TypeApprovalNumber}");
                Console.WriteLine($"Power/weight     : {card.PowerWeightRatio}");
                Console.WriteLine($"Restriction      : {card.RestrictionToChangeOwner}");

                Console.WriteLine();
                Console.WriteLine("══════════════════════════════════");
                Console.WriteLine("  OWNER");
                Console.WriteLine("══════════════════════════════════");
                Console.WriteLine($"Full name        : {card.OwnerFullName}");
                Console.WriteLine($"Personal number  : {card.OwnersPersonalNo}");
                Console.WriteLine($"Address          : {card.OwnersAddress}");

                if (!string.IsNullOrWhiteSpace(card.UsersPersonalNo) &&
                    card.UsersPersonalNo != card.OwnersPersonalNo)
                {
                    Console.WriteLine();
                    Console.WriteLine("══════════════════════════════════");
                    Console.WriteLine("  USER");
                    Console.WriteLine("══════════════════════════════════");
                    Console.WriteLine($"Full name        : {card.UserFullName}");
                    Console.WriteLine($"Personal number  : {card.UsersPersonalNo}");
                    Console.WriteLine($"Address          : {card.UsersAddress}");
                }
            }
            catch (CardNotFoundException ex)
            {
                Console.WriteLine($"[ERROR] Card not found: {ex.Message}");
            }
            catch (ReaderNotFoundException ex)
            {
                Console.WriteLine($"[ERROR] Reader not available: {ex.Message}");
            }
            catch (CardReadException ex)
            {
                Console.WriteLine($"[ERROR] Read error (0x{ex.ErrorCode:X8}): {ex.Message}");
            }

            Exit();
        }

        static void Exit()
        {
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}