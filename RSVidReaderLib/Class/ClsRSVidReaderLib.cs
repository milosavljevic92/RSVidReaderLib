using System;
using System.Runtime.InteropServices;
using System.Text;

namespace RSVidReaderLib
{
    public class CardNotFoundException : Exception
    {
        public CardNotFoundException() : base("Vehicle registration card not found in reader.") { }
    }

    public class ReaderNotFoundException : Exception
    {
        public ReaderNotFoundException() : base("Smart card reader not found or not connected.") { }
    }

    public class CardReadException : Exception
    {
        public int ErrorCode { get; }
        public CardReadException(string message, int code = 0) : base(message) { ErrorCode = code; }
        public CardReadException(string message, Exception inner) : base(message, inner) { }
    }

    public class RsVidData
    {
        // ── Document ──────────────────────────────────────────────────────────
        public string UnambiguousNumber { get; set; }   // Jedinstveni broj registracije
        public string IssuingDate { get; set; }
        public string ExpiryDate { get; set; }
        public string StateIssuing { get; set; }
        public string AuthorityIssuing { get; set; }
        public string CompetentAuthority { get; set; }
        public string SerialNumber { get; set; }

        // ── Vehicle ───────────────────────────────────────────────────────────
        public string RegistrationNumber { get; set; }   // Registarski broj
        public string VehicleIDNumber { get; set; }   // Broj šasije
        public string EngineIDNumber { get; set; }   // Broj motora
        public string VehicleMake { get; set; }   // Marka
        public string VehicleType { get; set; }   // Tip
        public string CommercialDescription { get; set; }   // Komercijalna oznaka (model)
        public string VehicleCategory { get; set; }   // Vrsta vozila
        public string ColourOfVehicle { get; set; }   // Boja
        public string DateOfFirstRegistration { get; set; }
        public string YearOfProduction { get; set; }
        public string EngineCapacity { get; set; }   // Kubikaza
        public string MaximumNetPower { get; set; }   // Snaga (kW)
        public string TypeOfFuel { get; set; }   // Vrsta goriva
        public string VehicleMass { get; set; }   // Sopstvena masa
        public string MaximumPermissibleLadenMass { get; set; }   // Najveca dozvoljena masa
        public string VehicleLoad { get; set; }   // Nosivost
        public string NumberOfSeats { get; set; }
        public string NumberOfStandingPlaces { get; set; }
        public string NumberOfAxles { get; set; }
        public string TypeApprovalNumber { get; set; }   // Homologacijska oznaka
        public string PowerWeightRatio { get; set; }   // Odnos snaga/masa
        public string RestrictionToChangeOwner { get; set; }   // Zabrana otudenja

        // ── Owner ─────────────────────────────────────────────────────────────
        public string OwnersPersonalNo { get; set; }   // JMBG vlasnika
        public string OwnersSurname { get; set; }
        public string OwnersName { get; set; }
        public string OwnersAddress { get; set; }

        // ── User (ako se razlikuje od vlasnika) ───────────────────────────────
        public string UsersPersonalNo { get; set; }
        public string UsersSurname { get; set; }
        public string UsersName { get; set; }
        public string UsersAddress { get; set; }

        // ── Computed ──────────────────────────────────────────────────────────
        public string OwnerFullName => $"{OwnersName} {OwnersSurname}".Trim();
        public string UserFullName => $"{UsersName} {UsersSurname}".Trim();

        public string IssuingDateFormatted => FormatDate(IssuingDate);
        public string ExpiryDateFormatted => FormatDate(ExpiryDate);
        public string FirstRegistrationFormatted => FormatDate(DateOfFirstRegistration);

        private static string FormatDate(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            if (raw.Contains('.') || raw.Contains('/') || raw.Contains('-')) return raw;
            if (raw.Length >= 8)
                return $"{raw.Substring(0, 2)}.{raw.Substring(2, 2)}.{raw.Substring(4, 4)}.";
            return raw;
        }
    }

    public sealed class RsVidReader : IDisposable
    {
        private const int BUFFER = 8192;

        private bool _started;
        private bool _disposed;

        public RsVidReader()
        {
            int ret = Native.sdStartup(1);
            if (ret != 0 && ret != 1056)
                throw new CardReadException("Initialization failed.", ret);
            _started = true;
        }

        // ── Jedini javni metod koji forma koristi ─────────────────────────────

        public RsVidData Read()
        {
            EnsureAlive();

            var nameBuf = new byte[256];
            int nameSize = nameBuf.Length;
            int ret = Native.GetReaderName(0, nameBuf, ref nameSize);
            if (ret != 0) throw new ReaderNotFoundException();

            string readerName = Encoding.Default.GetString(nameBuf, 0, Math.Max(0, nameSize - 1));

            ret = Native.SelectReader(readerName);
            if (ret != 0) throw new ReaderNotFoundException();

            ret = Native.sdProcessNewCard();
            MapError(ret);

            var data = new RsVidData();
            ReadDocument(data);
            ReadVehicle(data);
            ReadPersonal(data);
            return data;
        }

        // ── Static async helper ───────────────────────────────────────────────

        public static Task<RsVidData> ReadAsync()
        {
            var tcs = new TaskCompletionSource<RsVidData>();
            var thread = new Thread(() =>
            {
                try
                {
                    using (var r = new RsVidReader())
                        tcs.SetResult(r.Read());
                }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return tcs.Task;
        }

        public static bool HasReader()
        {
            try
            {
                int startup = Native.sdStartup(1);
                if (startup != 0 && startup != 1056) return false;
                var buf = new byte[256];
                int size = buf.Length;
                int ret = Native.GetReaderName(0, buf, ref size);
                if (startup != 1056) Native.sdCleanup();
                return ret == 0;
            }
            catch { return false; }
        }

        public static bool HasCard() => WinSCard.HasCard();

        // ── Čitanje blokova ───────────────────────────────────────────────────
        //
        //  Offseti su izračunati iz dokumentacije (Pack=1):
        //  svako polje = [char array] + [int size]

        private void ReadDocument(RsVidData d)
        {
            // SD_DOCUMENT_DATA
            // [50+4] stateIssuing          = 0
            // [50+4] competentAuthority    = 54
            // [50+4] authorityIssuing      = 108
            // [30+4] unambiguousNumber     = 162
            // [16+4] issuingDate           = 196
            // [16+4] expiryDate            = 216
            // [20+4] serialNumber          = 236
            UseBuffer(ptr =>
            {
                int ret = Native.sdReadDocumentData(ptr);
                if (ret != 0) throw new CardReadException("Failed to read document data.", ret);

                d.StateIssuing = Latin(At(ptr, 0, 50));
                d.CompetentAuthority = Latin(At(ptr, 54, 50));
                d.AuthorityIssuing = Latin(At(ptr, 108, 50));
                d.UnambiguousNumber = At(ptr, 162, 30);
                d.IssuingDate = At(ptr, 196, 16);
                d.ExpiryDate = At(ptr, 216, 16);
                d.SerialNumber = At(ptr, 236, 20);
            });
        }

        private void ReadVehicle(RsVidData d)
        {
            // SD_VEHICLE_DATA
            // [16+4] dateOfFirstRegistration     = 0
            // [5+4]  yearOfProduction             = 20
            // [100+4] vehicleMake                = 29
            // [100+4] vehicleType                = 133
            // [100+4] commercialDescription      = 237
            // [100+4] vehicleIDNumber            = 341
            // [20+4]  registrationNumberOfVehicle= 445
            // [20+4]  maximumNetPower            = 469
            // [20+4]  engineCapacity             = 493
            // [100+4] typeOfFuel                 = 517
            // [20+4]  powerWeightRatio           = 621
            // [20+4]  vehicleMass                = 645
            // [20+4]  maximumPermissibleLadenMass= 669
            // [50+4]  typeApprovalNumber         = 693
            // [20+4]  numberOfSeats              = 747
            // [20+4]  numberOfStandingPlaces     = 771
            // [100+4] engineIDNumber             = 795
            // [20+4]  numberOfAxles              = 899
            // [50+4]  vehicleCategory            = 923
            // [50+4]  colourOfVehicle            = 977
            // [200+4] restrictionToChangeOwner   = 1031
            // [20+4]  vehicleLoad               = 1235
            UseBuffer(ptr =>
            {
                int ret = Native.sdReadVehicleData(ptr);
                if (ret != 0) throw new CardReadException("Failed to read vehicle data.", ret);

                d.DateOfFirstRegistration = At(ptr, 0, 16);
                d.YearOfProduction = At(ptr, 20, 5);
                d.VehicleMake = Latin(At(ptr, 29, 100));
                d.VehicleType = Latin(At(ptr, 133, 100));
                d.CommercialDescription = Latin(At(ptr, 237, 100));
                d.VehicleIDNumber = At(ptr, 341, 100);
                d.RegistrationNumber = At(ptr, 445, 20);
                d.MaximumNetPower = At(ptr, 469, 20);
                d.EngineCapacity = At(ptr, 493, 20);
                d.TypeOfFuel = Latin(At(ptr, 517, 100));
                d.PowerWeightRatio = At(ptr, 621, 20);
                d.VehicleMass = At(ptr, 645, 20);
                d.MaximumPermissibleLadenMass = At(ptr, 669, 20);
                d.TypeApprovalNumber = At(ptr, 693, 50);
                d.NumberOfSeats = At(ptr, 747, 20);
                d.NumberOfStandingPlaces = At(ptr, 771, 20);
                d.EngineIDNumber = At(ptr, 795, 100);
                d.NumberOfAxles = At(ptr, 899, 20);
                d.VehicleCategory = Latin(At(ptr, 923, 50));
                d.ColourOfVehicle = Latin(At(ptr, 977, 50));
                d.RestrictionToChangeOwner = Latin(At(ptr, 1031, 200));
                d.VehicleLoad = At(ptr, 1235, 20);
            });
        }

        private void ReadPersonal(RsVidData d)
        {
            // SD_PERSONAL_DATA
            // [20+4]  ownersPersonalNo                 = 0
            // [100+4] ownersSurnameOrBusinessName      = 24
            // [100+4] ownerName                        = 128
            // [200+4] ownerAddress                     = 232
            // [20+4]  usersPersonalNo                  = 436
            // [100+4] usersSurnameOrBusinessName       = 460
            // [100+4] usersName                        = 564
            // [200+4] usersAddress                     = 668
            UseBuffer(ptr =>
            {
                int ret = Native.sdReadPersonalData(ptr);
                if (ret != 0) throw new CardReadException("Failed to read personal data.", ret);

                d.OwnersPersonalNo = At(ptr, 0, 20);
                d.OwnersSurname = Latin(At(ptr, 24, 100));
                d.OwnersName = Latin(At(ptr, 128, 100));
                d.OwnersAddress = Latin(At(ptr, 232, 200));
                d.UsersPersonalNo = At(ptr, 436, 20);
                d.UsersSurname = Latin(At(ptr, 460, 100));
                d.UsersName = Latin(At(ptr, 564, 100));
                d.UsersAddress = Latin(At(ptr, 668, 200));
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void UseBuffer(Action<IntPtr> action)
        {
            IntPtr ptr = Marshal.AllocHGlobal(BUFFER);
            try
            {
                for (int i = 0; i < BUFFER; i++) Marshal.WriteByte(ptr, i, 0);
                action(ptr);
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }

        private static string At(IntPtr ptr, int offset, int maxLen)
        {
            var bytes = new byte[maxLen];
            for (int i = 0; i < maxLen; i++)
                bytes[i] = Marshal.ReadByte(ptr, offset + i);
            int len = Array.IndexOf(bytes, (byte)0);
            if (len < 0) len = maxLen;
            if (len == 0) return string.Empty;
            return Encoding.UTF8.GetString(bytes, 0, len).Trim();
        }

        private static string Latin(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? string.Empty;
            bool cyrillic = false;
            foreach (char c in s)
                if (c >= '\u0400' && c <= '\u04FF') { cyrillic = true; break; }
            if (!cyrillic) return s;

            (string c, string l)[] map =
            {
                ("Љ","Lj"), ("Њ","Nj"), ("Џ","Dž"),
                ("љ","lj"), ("њ","nj"), ("џ","dž"),
                ("А","A"),  ("Б","B"),  ("В","V"),  ("Г","G"),
                ("Д","D"),  ("Ђ","Đ"),  ("Е","E"),  ("Ж","Ž"),
                ("З","Z"),  ("И","I"),  ("Ј","J"),  ("К","K"),
                ("Л","L"),  ("М","M"),  ("Н","N"),  ("О","O"),
                ("П","P"),  ("Р","R"),  ("С","S"),  ("Т","T"),
                ("Ћ","Ć"),  ("У","U"),  ("Ф","F"),  ("Х","H"),
                ("Ц","C"),  ("Ч","Č"),  ("Ш","Š"),
                ("а","a"),  ("б","b"),  ("в","v"),  ("г","g"),
                ("д","d"),  ("ђ","đ"),  ("е","e"),  ("ж","ž"),
                ("з","z"),  ("и","i"),  ("ј","j"),  ("к","k"),
                ("л","l"),  ("м","m"),  ("н","n"),  ("о","o"),
                ("п","p"),  ("р","r"),  ("с","s"),  ("т","t"),
                ("ћ","ć"),  ("у","u"),  ("ф","f"),  ("х","h"),
                ("ц","c"),  ("ч","č"),  ("ш","š"),
            };
            var sb = new StringBuilder(s);
            foreach (var (cyr, lat) in map)
                sb.Replace(cyr, lat);
            return sb.ToString();
        }

        private static void MapError(int ret)
        {
            uint code = (uint)ret;
            switch (code)
            {
                case 0: return;
                case 0x8010000C: throw new CardNotFoundException();   // SCARD_E_NO_SMARTCARD
                case 0x80100009: throw new ReaderNotFoundException(); // SCARD_E_UNKNOWN_READER
                case 0x8010002E: throw new ReaderNotFoundException(); // SCARD_E_NO_READERS_AVAILABLE
                case 0x8010001C: throw new CardNotFoundException();   // SCARD_E_CARD_UNSUPPORTED
                default: throw new CardReadException($"Card error (code: 0x{code:X8}).", ret);
            }
        }

        private void EnsureAlive()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(RsVidReader));
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_started) { try { Native.sdCleanup(); } catch { } }
            _disposed = true;
        }
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    internal static class Native
    {
        private const string DLL = "eVehicleRegistrationAPI.dll";
        private const CallingConvention CC = CallingConvention.Cdecl;

        [DllImport(DLL, CallingConvention = CC)]
        public static extern int sdStartup(int version);

        [DllImport(DLL, CallingConvention = CC)]
        public static extern int sdCleanup();

        [DllImport(DLL, CallingConvention = CC)]
        public static extern int GetReaderName(int index,
            [Out] byte[] readerName, ref int nameSize);

        [DllImport(DLL, CallingConvention = CC, CharSet = CharSet.Ansi)]
        public static extern int SelectReader(
            [MarshalAs(UnmanagedType.LPStr)] string reader);

        [DllImport(DLL, CallingConvention = CC)]
        public static extern int sdProcessNewCard();

        [DllImport(DLL, CallingConvention = CC)]
        public static extern int sdReadDocumentData(IntPtr data);

        [DllImport(DLL, CallingConvention = CC)]
        public static extern int sdReadVehicleData(IntPtr data);

        [DllImport(DLL, CallingConvention = CC)]
        public static extern int sdReadPersonalData(IntPtr data);

        [DllImport(DLL, CallingConvention = CC)]
        public static extern int sdReadRegistration(IntPtr data, int index);
    }

    // ── WinSCard ──────────────────────────────────────────────────────────────

    internal static class WinSCard
    {
        private const uint SCARD_SCOPE_USER = 0;
        private const uint SCARD_SHARE_SHARED = 2;
        private const uint SCARD_PROTOCOL_Tx = 3;
        private const uint SCARD_LEAVE_CARD = 0;

        [DllImport("winscard.dll")]
        static extern int SCardEstablishContext(uint scope, IntPtr r1, IntPtr r2, out IntPtr ctx);

        [DllImport("winscard.dll")]
        static extern int SCardReleaseContext(IntPtr ctx);

        [DllImport("winscard.dll")]
        static extern int SCardListReadersA(IntPtr ctx, string groups, byte[] buf, ref int len);

        [DllImport("winscard.dll")]
        static extern int SCardConnectA(IntPtr ctx, string reader, uint share, uint protocol,
            out IntPtr card, out uint activeProtocol);

        [DllImport("winscard.dll")]
        static extern int SCardDisconnect(IntPtr card, uint disposition);

        public static bool HasCard()
        {
            if (SCardEstablishContext(SCARD_SCOPE_USER, IntPtr.Zero, IntPtr.Zero, out var ctx) != 0)
                return false;
            try
            {
                int len = 0;
                if (SCardListReadersA(ctx, null, null, ref len) != 0 || len <= 2) return false;
                var buf = new byte[len];
                SCardListReadersA(ctx, null, buf, ref len);
                string reader = Encoding.Default.GetString(buf).Split('\0')[0];
                if (string.IsNullOrEmpty(reader)) return false;
                int ret = SCardConnectA(ctx, reader, SCARD_SHARE_SHARED, SCARD_PROTOCOL_Tx,
                    out IntPtr card, out uint _);
                if (ret == 0) { SCardDisconnect(card, SCARD_LEAVE_CARD); return true; }
                return false;
            }
            finally { SCardReleaseContext(ctx); }
        }
    }
}