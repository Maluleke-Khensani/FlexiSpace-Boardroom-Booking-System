namespace Flexispace.Mobile.Services;

/// <summary>
/// Protection of Personal Information policy sourced from
/// https://flexispace.net.za/popia-disclaimer/ (Flexispace Holdings (Pty) Ltd).
/// </summary>
public static class PrivacyPolicyContent
{
    public const string SourceUrl = "https://flexispace.net.za/popia-disclaimer/";
    public const string PopiaInfoUrl = "https://popia.co.za/";
    public const string CompanyName = "Flexispace Holdings (Pty) Ltd";
    public const string RegistrationNumber = "2015/384746/07";
    public const string ContactEmail = "info@flexispace.net.za";

    public const string Intro =
        "The directors of Flexispace Holdings (Pty) Ltd, Registration Number: 2015/384746/07 " +
        "do hereby confirm that the cornerstone of any implementation plan is the adherence " +
        "to the 8 conditions as stated in section 8 of the POPI Act.";

    public const string Conclusion =
        "Flexispace Holdings (Pty) Ltd will implement the above plan to the best of our ability. " +
        "We do recognize this as a fluid document that will be reviewed on a regular basis, " +
        "and we will be guided by any changes in the law as well as constant communication " +
        "with our Clients thereby re-assessing their needs from time to time.";

    public static IReadOnlyList<PrivacySection> Sections { get; } =
    [
        new(
            "1. Accountability",
            "Flexispace Holdings (Pty) Ltd are committed to compliance with, and adherence to the Act at all times. " +
            "This will be achieved by regularly assessing the personal information we store by conducting PIIA " +
            "(Personal Information Impact Assessments), awareness sessions and training of our staff."),

        new(
            "2. Processing limitation",
            "The personal information must be processed lawfully in a manner that does not infringe on the " +
            "Data Subject’s (Client’s) privacy and in a manner that is adequate, relevant, and not excessive " +
            "and complies with S11 and S12 of the POPIA. We will ensure that only relevant information is " +
            "collected for us to communicate with the Data Subjects in relation to the following:\n\n" +
            "• Contracts\n" +
            "• Full name\n" +
            "• ID\n" +
            "• Address\n" +
            "• Email address\n" +
            "• Telephone numbers\n" +
            "• CIPC documents\n" +
            "• Biometrics (Fingerprints/Printer codes)\n" +
            "• CCTV footage\n" +
            "• Billing of rent/telephony/printing/boardroom use\n" +
            "• Collection of rent/telephony/printing/boardroom use\n" +
            "• BCC distribution lists for correspondence to Data Subjects from Management\n" +
            "• Legal debt recovery"),

        new(
            "3. Purpose Specification",
            "Personal information must be collected only for a specific, explicitly defined lawful purpose " +
            "related to the function of Flexispace Holdings (Pty) Ltd.\n\n" +
            "This information will only be collected as per the above."),

        new(
            "4. Further processing limitation",
            "The further processing of the personal information by Flexispace Holdings (Pty) Ltd must be " +
            "compatible with the purpose of collection.\n\n" +
            "When further processing is required by Flexispace Holdings (Pty) Ltd will take account of the following:\n\n" +
            "• The relationship between the purpose of the intended further processing and the purpose " +
            "following which the information has been collected\n" +
            "• The nature of the information concerned\n" +
            "• The consequences of the intended further processing for the Data Subject\n" +
            "• The way the information has been collected\n" +
            "• Any contractual rights and obligations between the parties"),

        new(
            "5. Information quality",
            "Flexispace Holdings (Pty) Ltd will take responsible steps to ensure that the personal information " +
            "is complete, accurate, not misleading and updated where necessary.\n\n" +
            "Due to the nature of Flexispace rentals, information changes from time to time. Flexispace Holdings " +
            "(Pty) Ltd runs off the premier property management software programme MDA which will alert the " +
            "Operator as soon as an e-mail is not delivered due to an incorrect address."),

        new(
            "6. Openness",
            "Flexispace Holdings (Pty) Ltd must maintain the documentation of the processing operations " +
            "(the activity to collect the personal information including receipt, recording, organization, " +
            "cancellation, storage and use as well as transmission, re-formatting, merging and destruction).\n\n" +
            "Flexispace Holdings (Pty) Ltd will advise Data Subjects of the reasons why we are collecting any " +
            "personal information as well the necessary authorization by completing the required documentation " +
            "as listed in the POPI manual."),

        new(
            "7. Security safeguards",
            "Flexispace Holdings (Pty) Ltd must secure the integrity and confidentiality of the personal " +
            "information by taking reasonable technical and organizational measures to prevent loss, damage, " +
            "unauthorized destruction as well as unlawful access.\n\n" +
            "Flexispace stores information at three separate locations currently:\n\n" +
            "• Cloud\n" +
            "• Local Hard Drives (Desktops/laptops)\n" +
            "• Hard copies\n\n" +
            "a. Cloud — we use third party encryption processes to restrict access remotely. We are currently " +
            "developing sophisticated software that will only allow our operators to access the information in " +
            "the cloud once an OTP has been sent to their mobile devices. This together with regular password " +
            "changes (every 3 months) should safeguard the information stored in the location adequately.\n\n" +
            "b. Local hard drives — Flexispace Holdings is looking at completely disposing of the method of " +
            "storage and only using the cloud in future. This is a work in progress and until we achieve this, " +
            "the following precautions have been put in place:\n\n" +
            "1. All devices are locked away in a private office when not in use.\n" +
            "2. All devices will be powered down when not in use and a password is required to access the " +
            "system when it is switched on.\n" +
            "3. No staff are allowed to use any access ports without prior authorization from the Director, " +
            "stating very clearly the need to do so.\n" +
            "4. Up to date virus protectors are installed on a regular basis to prevent any damage.\n" +
            "5. When in use the devices are protected by a state-of-the-art Firewall that governs access to " +
            "the Wi-Fi network.\n\n" +
            "c. Hard copies — these are stored in a secure filing room. Access is limited to one staff member " +
            "at a time. A register is kept detailing the operator’s name, day, time, reason for the access to " +
            "the information. The return date and time are also recorded in the register. The keys to the " +
            "filing room are kept in a locked drawer by the Administration Manager. All operators will sign a " +
            "non-disclosure agreement to prevent the sharing of personal information with third parties. These " +
            "processes will constantly be reviewed by conducting regular PIAAs."),

        new(
            "8. Data Subject participation",
            "The Data Subject has the right to request Flexispace Holdings (Pty) Ltd free of charge to advise " +
            "whether they hold personal information and to request the record of the personal information " +
            "within a reasonable time frame. Flexispace Holdings (Pty) Ltd will allow a Data Subject access to " +
            "his/her personal information by first confirming the identity of the Subject, by performing a " +
            "quick verification process. The Subject will be expected to provide details that are currently " +
            "on record such as location, email address and contact number.\n\n" +
            "Once the Operator is satisfied that the Data Subject has been verified, they will be requested " +
            "to complete the necessary documentation required, detailing their reason for requiring the " +
            "information. The Data Subject may also request the Operator to change or destroy the personal " +
            "information on record. In this case the prescribed forms will be completed stating the reason " +
            "for the request.")
    ];
}

public sealed record PrivacySection(string Title, string Body);
