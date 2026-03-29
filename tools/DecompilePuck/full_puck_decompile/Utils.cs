using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

public static class Utils
{
	public static Dictionary<string, string> CountryDictionary = new Dictionary<string, string>
	{
		{ "AD", "Andorra" },
		{ "AE", "United Arab Emirates" },
		{ "AF", "Afghanistan" },
		{ "AG", "Antigua and Barbuda" },
		{ "AI", "Anguilla" },
		{ "AL", "Albania" },
		{ "AM", "Armenia" },
		{ "AO", "Angola" },
		{ "AQ", "Antarctica" },
		{ "AR", "Argentina" },
		{ "AS", "American Samoa" },
		{ "AT", "Austria" },
		{ "AU", "Australia" },
		{ "AW", "Aruba" },
		{ "AX", "Åland Islands" },
		{ "AZ", "Azerbaijan" },
		{ "BA", "Bosnia and Herzegovina" },
		{ "BB", "Barbados" },
		{ "BD", "Bangladesh" },
		{ "BE", "Belgium" },
		{ "BF", "Burkina Faso" },
		{ "BG", "Bulgaria" },
		{ "BH", "Bahrain" },
		{ "BI", "Burundi" },
		{ "BJ", "Benin" },
		{ "BL", "Saint Barthélemy" },
		{ "BM", "Bermuda" },
		{ "BN", "Brunei Darussalam" },
		{ "BO", "Plurinational State of Bolivia" },
		{ "BQ", "Caribbean Netherlands" },
		{ "BR", "Brazil" },
		{ "BS", "Bahamas" },
		{ "BT", "Bhutan" },
		{ "BV", "Bouvet Island" },
		{ "BW", "Botswana" },
		{ "BY", "Belarus" },
		{ "BZ", "Belize" },
		{ "CA", "Canada" },
		{ "CC", "Cocos (Keeling) Islands" },
		{ "CD", "Democratic Republic of Congo" },
		{ "CF", "Central African Republic" },
		{ "CG", "Republic of the Congo" },
		{ "CH", "Switzerland" },
		{ "CI", "Côte d'Ivoire" },
		{ "CK", "Cook Islands" },
		{ "CL", "Chile" },
		{ "CM", "Cameroon" },
		{ "CN", "China (People's Republic of China)" },
		{ "CO", "Colombia" },
		{ "CR", "Costa Rica" },
		{ "CU", "Cuba" },
		{ "CV", "Cape Verde" },
		{ "CW", "Curaçao" },
		{ "CX", "Christmas Island" },
		{ "CY", "Cyprus" },
		{ "CZ", "Czech Republic" },
		{ "DE", "Germany" },
		{ "DJ", "Djibouti" },
		{ "DK", "Denmark" },
		{ "DM", "Dominica" },
		{ "DO", "Dominican Republic" },
		{ "DZ", "Algeria" },
		{ "EC", "Ecuador" },
		{ "EE", "Estonia" },
		{ "EG", "Egypt" },
		{ "EH", "Western Sahara" },
		{ "ER", "Eritrea" },
		{ "ES", "Spain" },
		{ "ET", "Ethiopia" },
		{ "EU", "Europe" },
		{ "FI", "Finland" },
		{ "FJ", "Fiji" },
		{ "FK", "Falkland Islands (Malvinas)" },
		{ "FM", "Federated States of Micronesia" },
		{ "FO", "Faroe Islands" },
		{ "FR", "France" },
		{ "GA", "Gabon" },
		{ "GB-ENG", "England" },
		{ "GB-NIR", "Northern Ireland" },
		{ "GB-SCT", "Scotland" },
		{ "GB-WLS", "Wales" },
		{ "GB", "United Kingdom" },
		{ "GD", "Grenada" },
		{ "GE", "Georgia" },
		{ "GF", "French Guiana" },
		{ "GG", "Guernsey" },
		{ "GH", "Ghana" },
		{ "GI", "Gibraltar" },
		{ "GL", "Greenland" },
		{ "GM", "Gambia" },
		{ "GN", "Guinea" },
		{ "GP", "Guadeloupe" },
		{ "GQ", "Equatorial Guinea" },
		{ "GR", "Greece" },
		{ "GS", "South Georgia and the South Sandwich Islands" },
		{ "GT", "Guatemala" },
		{ "GU", "Guam" },
		{ "GW", "Guinea-Bissau" },
		{ "GY", "Guyana" },
		{ "HK", "Hong Kong" },
		{ "HM", "Heard Island and McDonald Islands" },
		{ "HN", "Honduras" },
		{ "HR", "Croatia" },
		{ "HT", "Haiti" },
		{ "HU", "Hungary" },
		{ "ID", "Indonesia" },
		{ "IE", "Ireland" },
		{ "IL", "Israel" },
		{ "IM", "Isle of Man" },
		{ "IN", "India" },
		{ "IO", "British Indian Ocean Territory" },
		{ "IQ", "Iraq" },
		{ "IR", "Islamic Republic of Iran" },
		{ "IS", "Iceland" },
		{ "IT", "Italy" },
		{ "JE", "Jersey" },
		{ "JM", "Jamaica" },
		{ "JO", "Jordan" },
		{ "JP", "Japan" },
		{ "KE", "Kenya" },
		{ "KG", "Kyrgyzstan" },
		{ "KH", "Cambodia" },
		{ "KI", "Kiribati" },
		{ "KM", "Comoros" },
		{ "KN", "Saint Kitts and Nevis" },
		{ "KP", "Democratic People's Republic of Korea" },
		{ "KR", "Republic of Korea" },
		{ "KW", "Kuwait" },
		{ "KY", "Cayman Islands" },
		{ "KZ", "Kazakhstan" },
		{ "LA", "Laos (Lao People's Democratic Republic)" },
		{ "LB", "Lebanon" },
		{ "LC", "Saint Lucia" },
		{ "LI", "Liechtenstein" },
		{ "LK", "Sri Lanka" },
		{ "LR", "Liberia" },
		{ "LS", "Lesotho" },
		{ "LT", "Lithuania" },
		{ "LU", "Luxembourg" },
		{ "LV", "Latvia" },
		{ "LY", "Libya" },
		{ "MA", "Morocco" },
		{ "MC", "Monaco" },
		{ "MD", "Republic of Moldova" },
		{ "ME", "Montenegro" },
		{ "MF", "Saint Martin" },
		{ "MG", "Madagascar" },
		{ "MH", "Marshall Islands" },
		{ "MK", "North Macedonia" },
		{ "ML", "Mali" },
		{ "MM", "Myanmar" },
		{ "MN", "Mongolia" },
		{ "MO", "Macao" },
		{ "MP", "Northern Mariana Islands" },
		{ "MQ", "Martinique" },
		{ "MR", "Mauritania" },
		{ "MS", "Montserrat" },
		{ "MT", "Malta" },
		{ "MU", "Mauritius" },
		{ "MV", "Maldives" },
		{ "MW", "Malawi" },
		{ "MX", "Mexico" },
		{ "MY", "Malaysia" },
		{ "MZ", "Mozambique" },
		{ "NA", "Namibia" },
		{ "NC", "New Caledonia" },
		{ "NE", "Niger" },
		{ "NF", "Norfolk Island" },
		{ "NG", "Nigeria" },
		{ "NI", "Nicaragua" },
		{ "NL", "Netherlands" },
		{ "NO", "Norway" },
		{ "NP", "Nepal" },
		{ "NR", "Nauru" },
		{ "NU", "Niue" },
		{ "NZ", "New Zealand" },
		{ "OM", "Oman" },
		{ "PA", "Panama" },
		{ "PE", "Peru" },
		{ "PF", "French Polynesia" },
		{ "PG", "Papua New Guinea" },
		{ "PH", "Philippines" },
		{ "PK", "Pakistan" },
		{ "PL", "Poland" },
		{ "PM", "Saint Pierre and Miquelon" },
		{ "PN", "Pitcairn" },
		{ "PR", "Puerto Rico" },
		{ "PS", "Palestine" },
		{ "PT", "Portugal" },
		{ "PW", "Palau" },
		{ "PY", "Paraguay" },
		{ "QA", "Qatar" },
		{ "RE", "Réunion" },
		{ "RO", "Romania" },
		{ "RS", "Serbia" },
		{ "RU", "Russian Federation" },
		{ "RW", "Rwanda" },
		{ "SA", "Saudi Arabia" },
		{ "SB", "Solomon Islands" },
		{ "SC", "Seychelles" },
		{ "SD", "Sudan" },
		{ "SE", "Sweden" },
		{ "SG", "Singapore" },
		{ "SH", "Saint Helena, Ascension and Tristan da Cunha" },
		{ "SI", "Slovenia" },
		{ "SJ", "Svalbard and Jan Mayen Islands" },
		{ "SK", "Slovakia" },
		{ "SL", "Sierra Leone" },
		{ "SM", "San Marino" },
		{ "SN", "Senegal" },
		{ "SO", "Somalia" },
		{ "SR", "Suriname" },
		{ "SS", "South Sudan" },
		{ "ST", "Sao Tome and Principe" },
		{ "SV", "El Salvador" },
		{ "SX", "Sint Maarten (Dutch part)" },
		{ "SY", "Syrian Arab Republic" },
		{ "SZ", "Swaziland" },
		{ "TC", "Turks and Caicos Islands" },
		{ "TD", "Chad" },
		{ "TF", "French Southern Territories" },
		{ "TG", "Togo" },
		{ "TH", "Thailand" },
		{ "TJ", "Tajikistan" },
		{ "TK", "Tokelau" },
		{ "TL", "Timor-Leste" },
		{ "TM", "Turkmenistan" },
		{ "TN", "Tunisia" },
		{ "TO", "Tonga" },
		{ "TR", "Turkey" },
		{ "TT", "Trinidad and Tobago" },
		{ "TV", "Tuvalu" },
		{ "TW", "Taiwan (Republic of China)" },
		{ "TZ", "United Republic of Tanzania" },
		{ "UA", "Ukraine" },
		{ "UG", "Uganda" },
		{ "UM", "US Minor Outlying Islands" },
		{ "US", "United States" },
		{ "UY", "Uruguay" },
		{ "UZ", "Uzbekistan" },
		{ "VA", "Holy See (Vatican City State)" },
		{ "VC", "Saint Vincent and the Grenadines" },
		{ "VE", "Bolivarian Republic of Venezuela" },
		{ "VG", "Virgin Islands, British" },
		{ "VI", "Virgin Islands, U.S." },
		{ "VN", "Vietnam" },
		{ "VU", "Vanuatu" },
		{ "WF", "Wallis and Futuna Islands" },
		{ "WS", "Samoa" },
		{ "XK", "Kosovo" },
		{ "YE", "Yemen" },
		{ "YT", "Mayotte" },
		{ "ZA", "South Africa" },
		{ "ZM", "Zambia" },
		{ "ZW", "Zimbabwe" }
	};

	public static string[] ProfanityWords = new string[2723]
	{
		"2 girls 1 cup", "2g1c", "4r5e", "5h1t", "5hit", "5ht", "@$$", "a s s", "a s shole", "a55",
		"a55hole", "a_s_s", "abbo", "abeed", "abuse", "acrotomophilia", "africoon", "ahole", "alabama hot pocket", "alaskan pipeline",
		"alligator bait", "alligatorbait", "amcik", "anal", "analannie", "analprobe", "analsex", "andskota", "anilingus", "anus",
		"apeshit", "ar5e", "arabush", "arabushs", "areola", "areole", "argie", "armo", "armos", "arrse",
		"arschloch", "arse", "arsehole", "aryan", "ash0le", "ash0les", "asholes", "ass monkey", "ass", "ass-fucker",
		"ass-hat", "ass-pirate", "assbag", "assbagger", "assbandit", "assbang", "assbanged", "assbanger", "assbangs", "assbite",
		"assblaster", "assclown", "asscock", "asscowboy", "asscracker", "asses", "assface", "assfuck", "assfucker", "assfukka",
		"assgoblin", "assh0le", "assh0lez", "asshat", "asshead", "assho1e", "asshole", "assholes", "assholz", "asshopper",
		"asshore", "assjacker", "assjockey", "asskiss", "asskisser", "assklown", "asslick", "asslicker", "asslover", "assman",
		"assmaster", "assmonkey", "assmunch", "assmuncher", "assnigger", "asspacker", "asspirate", "asspuppies", "assrammer", "assranger",
		"assshit", "assshole", "asssucker", "asswad", "asswhole", "asswhore", "asswipe", "asswipes", "auto erotic", "autoerotic",
		"ayir", "azazel", "azz", "azzhole", "b a s t a r d", "b i t c h", "b o o b", "b!+ch", "b!tch", "b!tchin",
		"b*tch", "b00b", "b00bies", "b00biez", "b00bs", "b00bz", "b17ch", "b1tch", "b7ch", "babeland",
		"babes", "baby batter", "baby juice", "backdoorman", "badfuck", "ball gag", "ball gravy", "ball kicking", "ball licking", "ball sack",
		"ball sucking", "ballbag", "balllicker", "ballsack", "bampot", "bangbro", "bangbros", "bangbus", "bareback", "barely legal",
		"barelylegal", "barenaked", "barface", "barfface", "bassterd", "bassterds", "bastard", "bastardo", "bastards", "bastardz",
		"basterds", "basterdz", "bastinado", "bazongas", "bazooms", "bbw", "bdsm", "beaner", "beaners", "beaney",
		"beaneys", "beardedclam", "beastality", "beastial", "beastiality", "beastility", "beatch", "beatoff", "beatyourmeat", "beaver cleaver",
		"beaver lips", "beef curtains", "beeyotch", "bellend", "beotch", "bestial", "bestiality", "bi curious", "bi+ch", "bi7ch",
		"biatch", "bicurious", "big black", "big breasts", "big knockers", "big tits", "bigass", "bigbastard", "bigbreasts", "bigbutt",
		"bigtits", "bimbo", "bimbos", "bint", "birdlock", "bitch", "bitchass", "bitched", "bitcher", "bitchers",
		"bitches", "bitchez", "bitchin", "bitching", "bitchslap", "bitchtit", "bitchy", "biteme", "bitties", "black cock",
		"blackcock", "blackman", "blacks", "blonde action", "blonde on blonde action", "blonde on blonde", "bloodclaat", "blow j", "blow job", "blow your l",
		"blow your load", "blowjob", "blowjobs", "blue waffle", "bluegum", "bluegums", "blumpkin", "bo ob", "bo obs", "boang",
		"boche", "boches", "boffing", "bogan", "bohunk", "boink", "boiolas", "bollick", "bollock", "bollocks",
		"bollok", "bollox", "bombers", "bomd", "bondage", "boned", "boner", "boners", "bong", "boong",
		"boonga", "boongas", "boongs", "boonie", "boonies", "booobs", "boooobs", "booooobs", "booooooobs", "bootee",
		"bootlip", "bootlips", "boozer", "bosch", "bosche", "bosches", "boschs", "bosomy", "bounty bar", "bounty bars",
		"bountybar", "brea5t", "breastjob", "breastlover", "breastman", "brown shower", "brown showers", "brunette action", "btch", "buceta",
		"buddhahead", "buddhaheads", "buffies", "bugger", "buggered", "buggery", "bukake", "bukkake", "bullcrap", "bulldike",
		"bulldyke", "bullet vibe", "bullshit", "bullshits", "bullshitted", "bullturds", "bumblefuck", "bumfuck", "bung hole", "bung",
		"bunga", "bungas", "bunghole", "bunny fucker", "burr head", "burr heads", "burrhead", "burrheads", "butchbabes", "butchdike",
		"butchdyke", "butt plug", "butt-pirate", "buttbang", "buttcheeks", "buttface", "buttfuck", "buttfucker", "buttfuckers", "butthead",
		"butthole", "buttman", "buttmuch", "buttmunch", "buttmuncher", "buttpirate", "buttplug", "buttstain", "buttwipe", "byatch",
		"c u n t", "c-0-c-k", "c-o-c-k", "c-u-n-t", "c.0.c.k", "c.o.c.k.", "c.u.n.t", "c0ck", "c0cks", "c0cksucker",
		"c0k", "cabron", "caca", "cacker", "cahone", "camel jockey", "camel jockeys", "camel toe", "cameljockey", "cameltoe",
		"camgirl", "camslut", "camwhore", "carpet muncher", "carpetmuncher", "carruth", "cawk", "cawks", "cazzo", "chav",
		"cheese eating surrender monkey", "cheese eating surrender monkies", "cheeseeating surrender monkey", "cheeseeating surrender monkies", "cheesehead", "cheeseheads", "cherrypopper", "chickslick", "china swede", "china swedes",
		"chinaman", "chinamen", "chinaswede", "chinaswedes", "chinc", "chincs", "ching chong", "ching chongs", "chinga", "chingchong",
		"chingchongs", "chink", "chinks", "chinky", "choad", "chocolate rosebuds", "chode", "chodes", "chonkies", "chonky",
		"chonkys", "chraa", "christ killer", "christ killers", "chug", "chugs", "chuj", "chunger", "chungers", "chunkies",
		"chunkys", "cipa", "circlejerk", "cl1t", "clamdigger", "clamdiver", "clamps", "clansman", "clansmen", "clanswoman",
		"clanswomen", "cleveland steamer", "clit", "clitface", "clitfuck", "clitoris", "clitorus", "clits", "clitty", "clogwog",
		"clover clamps", "clusterfuck", "cnts", "cntz", "cnut", "cocain", "cocaine", "cock", "cock-head", "cock-sucker",
		"cockbite", "cockblock", "cockblocker", "cockburger", "cockcowboy", "cockface", "cockfight", "cockfucker", "cockhead", "cockholster",
		"cockjockey", "cockknob", "cockknocker", "cockknoker", "cocklicker", "cocklover", "cockmaster", "cockmongler", "cockmongruel", "cockmonkey",
		"cockmunch", "cockmuncher", "cocknob", "cocknose", "cocknugget", "cockqueen", "cockrider", "cocks", "cockshit", "cocksman",
		"cocksmith", "cocksmoker", "cocksucer", "cocksuck", "cocksucked", "cocksucker", "cocksucking", "cocksucks", "cocksuka", "cocksukka",
		"cocktease", "cocky", "cohee", "coital", "coitus", "cok", "cokmuncher", "coksucka", "condom", "coochie",
		"coochy", "coolie", "coolies", "cooly", "coon ass", "coon asses", "coonass", "coonasses", "coondog", "coons",
		"cooter", "coprolagnia", "coprophilia", "copulate", "corksucker", "cornhole", "cra5h", "crackcocain", "crackpipe", "crackwhore",
		"crap", "crapola", "crapper", "crappy", "creampie", "crotchjockey", "crotchmonkey", "crotchrot", "cuck", "cum face",
		"cum licker", "cum", "cumbubble", "cumdumpster", "cumfest", "cumguzzler", "cuming", "cumjockey", "cumlickr", "cumm",
		"cummer", "cummin", "cumming", "cumquat", "cumqueen", "cums", "cumshot", "cumshots", "cumslut", "cumstain",
		"cumsucker", "cumtart", "cunilingus", "cunillingus", "cunn", "cunnie", "cunnilingus", "cunntt", "cunny", "cunt",
		"cunteyed", "cuntface", "cuntfuck", "cuntfucker", "cunthole", "cunthunter", "cuntlick", "cuntlicker", "cuntlicking", "cuntrag",
		"cunts", "cuntslut", "cuntsucker", "cuntz", "curry muncher", "curry munchers", "currymuncher", "currymunchers", "cushi", "cushis",
		"cyalis", "cyberfuc", "cyberfuck", "cyberfucked", "cyberfucker", "cyberfuckers", "cyberfucking", "cybersex", "cyberslimer", "d0ng",
		"d0uch3", "d0uche", "d1ck", "d1ld0", "d1ldo", "d4mn", "dago", "dagos", "dahmer", "damm",
		"dammit", "damn", "damnit", "darkey", "darkeys", "darkie", "darkies", "darky", "date rape", "daterape",
		"datnigga", "dawgie style", "dawgie-style", "daygo", "deapthroat", "deep throat", "deep throating", "deepaction", "deepthroat", "deepthroating",
		"defecate", "deggo", "dego", "degos", "dendrophilia", "destroyyourpussy", "deth", "diaper daddy", "diaper head", "diaper heads",
		"diaperdaddy", "diaperhead", "diaperheads", "dick pic", "dick", "dick-ish", "dickbag", "dickbeater", "dickbeaters", "dickbrain",
		"dickdipper", "dickface", "dickflipper", "dickforbrains", "dickfuck", "dickhead", "dickheads", "dickhole", "dickish", "dickjuice",
		"dickless", "dicklick", "dicklicker", "dickman", "dickmilk", "dickmonger", "dickpic", "dickripper", "dicks", "dicksipper",
		"dickslap", "dickslicker", "dicksucker", "dickwad", "dickweasel", "dickweed", "dickwhipper", "dickwod", "dickzipper", "diddle",
		"dike", "dild0", "dild0s", "dildo", "dildos", "dilf", "diligaf", "dilld0", "dilld0s", "dillweed",
		"dimwit", "dingle", "dingleberries", "dingleberry", "dink", "dinks", "dipship", "dipshit", "dipstick", "dirsa",
		"dirty pillows", "dirty sanchez", "dix", "dixiedike", "dixiedyke", "dlck", "dog style", "dog-fucker", "doggie style", "doggie",
		"doggie-style", "doggiestyle", "doggin", "dogging", "doggy style", "doggy-style", "doggystyle", "dolcett", "dominatricks", "dominatrics",
		"dominatrix", "dommes", "dong", "donkey punch", "donkeypunch", "donkeyribber", "doochbag", "doodoo", "doofus", "dookie",
		"doosh", "dot head", "dot heads", "dothead", "dotheads", "double dong", "double penetration", "doubledong", "doublepenetration", "douch3",
		"douche bag", "douche", "douche-fag", "douchebag", "douchebags", "douchewaffle", "douchey", "dp action", "dpaction", "dragqueen",
		"dragqween", "dripdick", "dry hump", "dryhump", "duche", "dudette", "dumass", "dumb ass", "dumbass", "dumbasses",
		"dumbbitch", "dumbfuck", "dumbshit", "dumshit", "dune coon", "dune coons", "dupa", "dvda", "dyefly", "dyke",
		"dykes", "dziwka", "earotics", "easyslut", "eat my ass", "eat my", "eatadick", "eatballs", "eathairpie", "eatme",
		"eatmyass", "eatpussy", "ecchi", "ejackulate", "ejakulate", "ekrem", "ekto", "enculer", "enema", "erection",
		"ero", "erotic", "erotism", "esqua", "essohbee", "ethical slut", "evl", "excrement", "exkwew", "extacy",
		"extasy", "f u c k e r", "f u c k e", "f u c k", "f u k", "f*ck", "f-u-c-k", "f.u.c.k", "f4nny", "f_u_c_k",
		"facefucker", "fack", "faeces", "faen", "fag", "fag1t", "fagbag", "faget", "fagfucker", "fagg",
		"fagg1t", "fagged", "fagging", "faggit", "faggitt", "faggot", "faggotcock", "faggs", "fagit", "fagot",
		"fagots", "fags", "fagt", "fagtard", "fagz", "faig", "faigs", "faigt", "fanculo", "fannybandit",
		"fannyflaps", "fannyfucker", "fanyy", "fartknocker", "fastfuck", "fatah", "fatfuck", "fatfucker", "fatso", "fck",
		"fckcum", "fckd", "fcuk", "fcuker", "fcuking", "fecal", "feck", "fecker", "feg", "felatio",
		"felch", "felcher", "felching", "fellate", "fellatio", "feltch", "feltcher", "feltching", "female squirting", "femalesquirtin",
		"femalesquirting", "femdom", "fetish", "ficken", "figging", "fingerbang", "fingerfood", "fingerfuck", "fingerfucked", "fingerfucker",
		"fingerfuckers", "fingerfucking", "fingerfucks", "fingering", "fisted", "fister", "fistfuck", "fistfucked", "fistfucker", "fistfuckers",
		"fistfucking", "fistfuckings", "fistfucks", "fisting", "fisty", "fitt", "flamer", "flasher", "flikker", "flipping the bird",
		"flogthelog", "floo", "floozy", "flydie", "flydye", "foad", "fok", "fondle", "foobar", "fook",
		"fooker", "foot fetish", "footaction", "footfetish", "footfuck", "footfucker", "footjob", "footlicker", "footstar", "foreskin",
		"forni", "fornicate", "fotze", "foursome", "fourtwenty", "freakfuck", "freakyfucker", "freefuck", "freex", "frigg",
		"frigga", "frigger", "frotting", "fucck", "fuck", "fuck-tard", "fucka", "fuckable", "fuckass", "fuckbag",
		"fuckbitch", "fuckbook", "fuckboy", "fuckbrain", "fuckbuddy", "fuckbutt", "fuckd", "fucked", "fuckedup", "fucker",
		"fuckers", "fuckersucker", "fuckface", "fuckfest", "fuckfreak", "fuckfriend", "fuckhead", "fuckheads", "fuckher", "fuckhole",
		"fuckin", "fuckina", "fucking", "fuckingbitch", "fuckings", "fuckingshitmotherfucker", "fuckinnuts", "fuckinright", "fuckit", "fuckknob",
		"fuckme", "fuckmeat", "fuckmehard", "fuckmonkey", "fuckn", "fucknugget", "fucknut", "fucknuts", "fucknutt", "fucknutz",
		"fuckoff", "fuckpig", "fuckpuppet", "fuckr", "fucks", "fuckstick", "fucktard", "fucktards", "fucktoy", "fucktrophy",
		"fuckup", "fuckwad", "fuckwhit", "fuckwhore", "fuckwit", "fuckwitt", "fuckyomama", "fuckyou", "fudge packer", "fudgepacker",
		"fugly", "fuk", "fukah", "fuken", "fuker", "fukin", "fuking", "fukk", "fukkah", "fukken",
		"fukker", "fukkin", "fukking", "fuks", "fuktard", "fuktards", "fukwhit", "fukwit", "funfuck", "futanari",
		"futanary", "futkretzn", "fuuck", "fux", "fux0r", "fuxor", "fvck", "fvk", "fxck", "g-spot",
		"g00k", "gae", "gai", "gang bang", "gangbang", "gangbanged", "gangbanger", "gangbangs", "ganja", "gassyass",
		"gator bait", "gatorbait", "gay sex", "gayass", "gaybob", "gayboy", "gaydo", "gaygirl", "gaylord", "gaymuthafuckinwhore",
		"gays", "gaysex", "gaytard", "gaywad", "gayz", "geezer", "geni", "genital", "genitals", "getiton",
		"gey", "gfy", "ghay", "ghey", "giant cock", "gigolo", "ginzo", "ginzos", "gipp", "gippo",
		"gippos", "gipps", "girl on top", "girl on", "girls gone wild", "givehead", "glans", "glazeddonut", "goatcx", "goatse",
		"god dammit", "god damn", "god damnit", "god-dam", "god-damned", "godam", "godammit", "godamn", "godamnit", "goddam",
		"goddamit", "goddamm", "goddammit", "goddamn", "goddamned", "goddamnes", "goddamnit", "goddamnmuthafucker", "godsdamn", "gokkun",
		"golden shower", "goldenshower", "golliwog", "golliwogs", "gonad", "gonads", "gonorrehea", "gonzagas", "goo girl", "gooch",
		"goodpoop", "gook eye", "gook eyes", "gook", "gookeye", "gookeyes", "gookies", "gooks", "gooky", "gora",
		"goras", "goregasm", "gotohell", "goy", "goyim", "greaseball", "greaseballs", "groe", "groid", "groids",
		"grope", "grostulation", "group sex", "gspot", "gstring", "gtfo", "gub", "gubba", "gubbas", "gubs",
		"guido", "guiena", "guineas", "guizi", "gummer", "guro", "gwailo", "gwailos", "gweilo", "gweilos",
		"gyopo", "gyopos", "gyp", "gyped", "gypo", "gypos", "gypp", "gypped", "gyppie", "gyppies",
		"gyppo", "gyppos", "gyppy", "gyppys", "gypsys", "h e l l", "h o m", "h00r", "h0ar", "h0m0",
		"h0mo", "h0r", "h0re", "h4x0r", "hadji", "hadjis", "hairyback", "hairybacks", "haji", "hajis",
		"hajji", "hajjis", "half breed", "half caste", "halfbreed", "halfcaste", "hamas", "hamflap", "hand job", "handjob",
		"haole", "haoles", "hapa", "hard core", "hardcore", "hardcoresex", "hardon", "he11", "headfuck", "hebe",
		"hebes", "heeb", "heebs", "hells", "helvete", "hentai", "heroin", "herp", "herpes", "herpy",
		"heshe", "hijacking", "hillbillies", "hillbilly", "hindoo", "hiscock", "hitler", "hitlerism", "hitlerist", "hoare",
		"hobag", "hodgie", "hoer", "hoes", "holestuffer", "hom0", "homo", "homobangers", "homodumbshit", "homoey",
		"honger", "honkers", "honkey", "honkeys", "honkie", "honkies", "honky", "hooch", "hooker", "hookers",
		"hoor", "hoore", "hootch", "hooter", "hooters", "hore", "hori", "horis", "hork", "horndawg",
		"horndog", "horney", "horniest", "horny", "horseshit", "hosejob", "hoser", "hot carl", "hot chick", "hotcarl",
		"hotdamn", "hotpussy", "hotsex", "hottotrot", "how to kill", "how to murder", "howtokill", "howtomurdep", "huevon", "huge fat",
		"hugefat", "hui", "hummer", "humped", "humper", "humpher", "humphim", "humpin", "humping", "hussy",
		"hustler", "hymen", "hymie", "hymies", "iblowu", "ike", "ikes", "ikey", "ikeymo", "ikeymos",
		"ikwe", "illegals", "incest", "indon", "indons", "injun", "injuns", "insest", "intercourse", "intheass",
		"inthebuff", "israels", "j3rk0ff", "jack off", "jack-off", "jackass", "jackhole", "jackoff", "jackshit", "jacktheripper",
		"jail bait", "jailbait", "jap", "japcrap", "japie", "japies", "japs", "jebus", "jelly donut", "jerk off",
		"jerk-off", "jerk0ff", "jerked", "jerkoff", "jerries", "jerry", "jewboy", "jewed", "jewess", "jiga",
		"jigaboo", "jigaboos", "jigarooni", "jigaroonis", "jigg", "jigga", "jiggabo", "jiggaboo", "jiggabos", "jiggas",
		"jigger", "jiggerboo", "jiggers", "jiggs", "jiggy", "jigs", "jihad", "jijjiboo", "jijjiboos", "jimfish",
		"jisim", "jism", "jiss", "jiz", "jizim", "jizin", "jizjuice", "jizm", "jizn", "jizz",
		"jizzd", "jizzed", "jizzim", "jizzin", "jizzn", "jizzum", "jugg", "juggs", "jungle bunnies", "jungle bunny",
		"junglebunny", "junkie", "junky", "kacap", "kacapas", "kacaps", "kaffer", "kaffir", "kaffre", "kafir",
		"kanake", "kanker", "katsap", "katsaps", "kawk", "khokhol", "khokhols", "kigger", "kike", "kikes",
		"kimchis", "kinbaku", "kink", "kinkster", "kinky", "kinkyJesus", "kissass", "kiunt", "kkk", "klan",
		"klansman", "klansmen", "klanswoman", "klanswomen", "klootzak", "knobbing", "knobead", "knobed", "knobend", "knobhead",
		"knobjocky", "knobjokey", "knobz", "knockers", "knulle", "kock", "kondum", "kondums", "kooch", "kooches",
		"koon", "kootch", "krap", "krappy", "kraut", "krauts", "kuffar", "kuk", "kuksuger", "kum",
		"kumbubble", "kumbullbe", "kumer", "kummer", "kumming", "kums", "kunilingus", "kunnilingus", "kunt", "kunts",
		"kuntz", "kurac", "kurwa", "kushi", "kushis", "kusi", "kwa", "kwai lo", "kwai los", "kwif",
		"kyke", "kykes", "kyopo", "kyopos", "kyrpa", "l3i+ch", "l3i\\+ch", "l3itch", "labia", "lapdance",
		"leather restraint", "leather straight", "leatherrestraint", "lebos", "lech", "lemon party", "lemonparty", "leper", "lesbain", "lesbayn",
		"lesbin", "lesbo", "lesbos", "lez", "lezbe", "lezbefriends", "lezbian", "lezbians", "lezbo", "lezbos",
		"lezz", "lezzian", "lezzie", "lezzies", "lezzo", "lezzy", "libido", "licker", "licking", "lickme",
		"lilniglet", "limey", "limpdick", "limy", "lingerie", "lipshits", "lipshitz", "livesex", "loadedgun", "lolita",
		"lovebone", "lovegoo", "lovegun", "lovejuice", "lovemuscle", "lovepistol", "loverocket", "lowlife", "lsd", "lubejob",
		"lubra", "lucifer", "luckycammeltoe", "lugan", "lugans", "lusting", "lusty", "lynch", "m-fucking", "m0f0",
		"m0fo", "m45terbate", "ma5terb8", "ma5terbate", "mabuno", "mabunos", "macaca", "macacas", "mafugly", "magicwand",
		"mahbuno", "mahbunos", "make me come", "makemecome", "makemecum", "male squirting", "mamhoon", "mams", "manhater", "manpaste",
		"maricon", "maricón", "marijuana", "masochist", "masokist", "massa", "massterbait", "masstrbait", "masstrbate", "mastabate",
		"mastabater", "master-bate", "masterb8", "masterbaiter", "masterbat", "masterbat3", "masterbate", "masterbates", "masterbating", "masterbation",
		"masterbations", "masterblaster", "mastrabator", "masturbat", "masturbate", "masturbating", "masturbation", "mattressprincess", "mau mau", "mau maus",
		"maumau", "maumaus", "mcfagget", "meatbeatter", "meatrack", "menage", "merd", "mgger", "mggor", "mibun",
		"mick", "mickeyfinn", "mideast", "mierda", "milf", "mindfuck", "minge", "minger", "mo-fo", "mockey",
		"mockie", "mocky", "mof0", "mofo", "moky", "molest", "molestation", "molester", "molestor", "moneyshot",
		"mong", "monkleigh", "moolie", "moon cricket", "moon crickets", "mooncricket", "mooncrickets", "moron", "moskal", "moskals",
		"moslem", "mosshead", "motha fucker", "motha fuker", "motha fukkah", "motha fukker", "mothafuck", "mothafucka", "mothafuckas", "mothafuckaz",
		"mothafucked", "mothafucker", "mothafuckers", "mothafuckin", "mothafucking", "mothafuckings", "mothafucks", "mother fucker", "mother fukah", "mother fuker",
		"mother fukkah", "mother fukker", "mother-fucker", "motherfuck", "motherfucka", "motherfucked", "motherfucker", "motherfuckers", "motherfuckin", "motherfucking",
		"motherfuckings", "motherfuckka", "motherfucks", "motherfvcker", "motherlovebone", "mothrfucker", "mouliewop", "mound of venus", "moundofvenus", "mr hands",
		"mrhands", "mtherfucker", "mthrfuck", "mthrfucker", "mthrfucking", "mtrfck", "mtrfuck", "mtrfucker", "muff diver", "muff",
		"muffdive", "muffdiver", "muffdiving", "muffindiver", "mufflikcer", "muffpuff", "muie", "mulatto", "mulkku", "muncher",
		"munging", "munt", "munter", "muschi", "mutha fucker", "mutha fukah", "mutha fuker", "mutha fukkah", "mutha fukker", "muthafecker",
		"muthafuckaz", "muthafucker", "muthafuckker", "muther", "mutherfucker", "mutherfucking", "muthrfucking", "mzungu", "mzungus", "n1gga",
		"n1gger", "nigg3r", "n1gr", "nads", "naked", "nambla", "nastt", "nastybitch", "nastyho", "nastyslut",
		"nastywhore", "nawashi", "nazi", "nazis", "nazism", "necro", "needthedick", "negres", "negress", "negro",
		"negroes", "negroid", "negros", "neonazi", "nepesaurio", "nig nog", "nig", "niga", "nigar", "nigars",
		"nigas", "nigers", "nigette", "nigettes", "nigg", "nigg3r", "nigg4h", "nigga", "niggah", "niggahs",
		"niggar", "niggaracci", "niggard", "niggarded", "niggarding", "niggardliness", "niggardlinesss", "niggardly", "niggards", "niggars",
		"niggas", "niggaz", "nigger", "niggerhead", "niggerhole", "niggers", "niggle", "niggled", "niggles", "nigglings",
		"niggor", "niggress", "niggresses", "nigguh", "nigguhs", "niggur", "niggurs", "niglet", "nignog", "nigor",
		"nigors", "nigr", "nigra", "nigras", "nigre", "nigres", "nigress", "nigs", "nigur", "niiger",
		"niigr", "nimphomania", "nimrod", "ninny", "nipple", "nipplering", "nipples", "nips", "nittit", "nlgger",
		"nlggor", "nob jokey", "nob", "nobhead", "nobjocky", "nobjokey", "nofuckingway", "nog", "nookey", "nookie",
		"nooky", "noonan", "nooner", "nsfw images", "nsfw", "nudger", "nudie", "nudies", "numbnuts", "nut sack",
		"nutbutter", "nutfucker", "nutsack", "nutten", "nympho", "nymphomania", "o c k", "octopussy", "omorashi", "one cup two girls",
		"one guy one jar", "one guy", "one jar", "ontherag", "orafis", "orga", "orgasim", "orgasim;", "orgasims", "orgasm",
		"orgasmic", "orgasms", "orgasum", "orgies", "orgy", "oriface", "orifiss", "orospu", "osama", "ovum",
		"ovums", "p e n i s", "p i s", "p u s s y", "p.u.s.s.y.", "p0rn", "packi", "packie", "packy", "paddy",
		"paedophile", "paki", "pakie", "pakis", "paky", "palesimian", "pancake face", "pancake faces", "panooch", "pansies",
		"pansy", "panti", "pantie", "panties", "panty", "paska", "payo", "pcp", "pearlnecklace", "pecker",
		"peckerhead", "peckerwood", "pedo", "pedobear", "pedophile", "pedophilia", "pedophiliac", "peeenus", "peeenusss", "peehole",
		"peenus", "peepee", "peepshow", "peepshpw", "pegging", "peinus", "pen1s", "penas", "pendejo", "pendy",
		"penetrate", "penetration", "peni5", "penial", "penile", "penis", "penis-breath", "penises", "penisfucker", "penisland",
		"penislick", "penislicker", "penispuffer", "penthouse", "penus", "penuus", "perse", "perv", "perversion", "peyote",
		"phalli", "phallic", "phone sex", "phonesex", "phuc", "phuck", "phuk", "phuked", "phuker", "phuking",
		"phukked", "phukker", "phukking", "phuks", "phungky", "phuq", "pi55", "picaninny", "piccaninny", "picka",
		"pickaninnies", "pickaninny", "piece of shit", "pieceofshit", "piefke", "piefkes", "pierdol", "pigfucker", "piker", "pikey",
		"piky", "pillowbiter", "pillu", "pimmel", "pimp", "pimped", "pimper", "pimpis", "pimpjuic", "pimpjuice",
		"pimpsimp", "pindick", "pinko", "pis", "pises", "pisin", "pising", "pisof", "piss pig", "piss",
		"piss-off", "pissed", "pisser", "pissers", "pisses", "pissflap", "pissflaps", "pisshead", "pissin", "pissing",
		"pissoff", "pisspig", "pizda", "playboy", "playgirl", "pleasure chest", "pleasurechest", "pocha", "pochas", "pocho",
		"pochos", "pocketpool", "pohm", "pohms", "polac", "polack", "polacks", "polak", "pole smoker", "polesmoker",
		"pollock", "pollocks", "pommie grant", "pommie grants", "pommy", "ponyplay", "poof", "poon", "poonani", "poonany",
		"poontang", "poontsee", "poop chute", "poopchute", "pooper", "pooperscooper", "pooping", "poorwhitetrash", "popimp", "porch monkey",
		"porch monkies", "porchmonkey", "porn", "pornflick", "pornking", "porno", "pornography", "pornos", "pornprincess", "pound town",
		"poundtown", "pplicker", "pr0n", "pr1c", "pr1ck", "pr1k", "prairie nigger", "prairie niggers", "preteen", "pric",
		"prickhead", "pricks", "prig", "prince albert piercing", "pron", "prostitute", "pthc", "pu55i", "pu55y", "pube",
		"pubes", "pubic", "pubiclice", "pubis", "pudboy", "pudd", "puddboy", "pula", "punani", "punanny",
		"punany", "punkass", "punky", "punta", "puntang", "purinapricness", "pusies", "puss", "pusse", "pussee",
		"pussi", "pussie", "pussies", "pussy", "pussycat", "pussydestroyer", "pussyeater", "pussyfart", "pussyfuck", "pussyfucker",
		"pussylicker", "pussylicking", "pussylips", "pussylover", "pussypalace", "pussypounder", "pussys", "pusy", "puta", "puto",
		"puuke", "puuker", "qahbeh", "quashie", "queaf", "queef", "queerhole", "queero", "queers", "queerz",
		"quickie", "quicky", "quiff", "quim", "qweers", "qweerz", "qweir", "r-tard", "r-tards", "r5e",
		"ra8s", "raghead", "ragheads", "rape", "raped", "raper", "raping", "rapist", "rautenberg", "rearend",
		"rearentry", "recktum", "rectal", "rectum", "rectus", "redleg", "redlegs", "redlight", "redskin", "redskins",
		"reefer", "reestie", "reetard", "reich", "renob", "rentafuck", "rere", "retard", "retarded", "retards",
		"retardz", "reverse cowgirl", "reversecowgirl", "rimjaw", "rimjob", "rimming", "ritard", "rosebuds", "rosy palm and her 5 sisters", "rosy palm",
		"rosypalm", "rosypalmandher5sisters", "rosypalmandherefivesisters", "round eyes", "roundeye", "rtard", "rtards", "rumprammer", "ruski", "russki",
		"russkie", "rusty trombone", "rustytrombone", "s h i t", "s hit", "s&m", "s-h-1-t", "s-h-i-t", "s-o-b", "s.h.i.t.",
		"s.o.b.", "s0b", "s_h_i_t", "sadis", "sadism", "sadist", "sadom", "sambo", "sambos", "samckdaddy",
		"sanchez", "sand nigger", "sand niggers", "sandm", "sandnigger", "santorum", "sausagequeen", "scag", "scallywag", "scank",
		"scantily", "scat", "schaffer", "scheiss", "schizo", "schlampe", "schlong", "schmuck", "schvartse", "schvartsen",
		"schwartze", "schwartzen", "scissoring", "screwyou", "scroat", "scrog", "scrote", "scrotum", "scrud", "seduce",
		"semen", "seppo", "seppos", "septics", "sex", "sexcam", "sexed", "sexfarm", "sexhound", "sexhouse",
		"sexi", "sexing", "sexkitten", "sexo", "sexpot", "sexslave", "sextogo", "sextoy", "sextoys", "sexual",
		"sexually", "sexwhore", "sexx", "sexxi", "sexxx", "sexxxi", "sexxxy", "sexxy", "sexy", "sexymoma",
		"sexyslim", "sh!+", "sh!t", "sh1t", "sh1ter", "sh1ts", "sh1tter", "sh1tz", "shag", "shagger",
		"shaggin", "shagging", "shamedame", "sharmuta", "sharmute", "shat", "shav", "shaved beaver", "shaved pussy", "shavedbeaver",
		"shavedpussy", "shawtypimp", "sheeney", "shemale", "shhit", "shi+", "shibari", "shibary", "shinola", "shipal",
		"shit ass", "shit", "shit-ass", "shit-bag", "shit-bagger", "shit-brain", "shit-breath", "shit-cunt", "shit-dick", "shit-eating",
		"shit-face", "shit-faced", "shit-fit", "shit-head", "shit-heel", "shit-hole", "shit-house", "shit-load", "shit-pot", "shit-spitter",
		"shit-stain", "shitass", "shitbag", "shitbagger", "shitblimp", "shitbrain", "shitbreath", "shitcan", "shitcunt", "shitdick",
		"shite", "shiteater", "shiteating", "shited", "shitey", "shitface", "shitfaced", "shitfit", "shitforbrains", "shitfuck",
		"shitfucker", "shitfull", "shithapens", "shithappens", "shithead", "shitheel", "shithole", "shithouse", "shiting", "shitings",
		"shitlist", "shitload", "shitola", "shitoutofluck", "shitpot", "shits", "shitspitter", "shitstain", "shitt", "shitted",
		"shitter", "shitters", "shittiest", "shitting", "shittings", "shitty", "shity", "shitz", "shiz", "shiznit",
		"shortfuck", "shota", "shylock", "shylocks", "shyt", "shyte", "shytty", "shyty", "simp", "sissy",
		"sixsixsix", "sixtynine", "sixtyniner", "skag", "skanck", "skank", "skankbitch", "skankee", "skankey", "skankfuck",
		"skanks", "skankwhore", "skanky", "skankybitch", "skankywhore", "skeet", "skinflute", "skribz", "skullfuck", "skum",
		"skumbag", "skurwysyn", "skwa", "skwe", "slag", "slanteye", "slanty", "slapper", "sleezeball", "slideitin",
		"slimeball", "slimebucket", "slopehead", "slopeheads", "sloper", "slopers", "slopey", "slopeys", "slopies", "slopy",
		"slut", "slutbag", "slutbucket", "slutdumper", "slutkiss", "sluts", "slutt", "slutting", "slutty", "slutwear",
		"slutwhore", "slutz", "smackthemonkey", "smeg", "smegma", "smut", "smutty", "snatchpatch", "sniggered", "sniggering",
		"sniggers", "snowback", "snowballing", "snownigger", "snuff", "socksucker", "sodom", "sodomise", "sodomite", "sodomize",
		"sodomy", "son of a bitch", "son of a whore", "son-of-a-bitch", "son-of-a-whore", "sonofabitch", "sonofbitch", "sooties", "souse", "soused",
		"soyboy", "spac", "spaghettibender", "spaghettinigger", "spank", "spankthemonkey", "spastic", "spearchucker", "spearchuckers", "sperm",
		"spermacide", "spermbag", "spermhearder", "spermherder", "sphencter", "spic", "spick", "spicks", "spics", "spierdalaj",
		"spig", "spigotty", "spik", "spiks", "splittail", "splooge", "spludge", "spooge", "spread legs", "spreadeagle",
		"spunk", "spunky", "sqeh", "squa", "squarehead", "squareheads", "squaw", "squinty", "squirting", "stagg",
		"stfu", "stiffy", "stoned", "stoner", "strap on", "strapon", "strappado", "strip club", "stripclub", "stroking",
		"stuinties", "stupidfuck", "stupidfucker", "style doggy", "suckdick", "sucked", "sucker", "sucking", "suckme", "suckmyass",
		"suckmydick", "suckmytit", "suckoff", "suicide girl", "suicide girls", "suicidegirl", "suicidegirls", "suka", "sultrywoman", "sultrywomen",
		"sumofabiatch", "swallower", "swalow", "swamp guinea", "swamp guineas", "swastika", "syphilis", "t i t", "t i ts", "t1t",
		"t1tt1e5", "t1tties", "tacohead", "tacoheads", "taff", "take off your", "tar babies", "tar baby", "tarbaby", "tard",
		"taste my", "tastemy", "tawdry", "tea bagging", "teabagging", "teat", "teets", "teez", "terd", "teste",
		"testee", "testes", "testical", "testicle", "testicles", "testis", "thicklip", "thicklips", "thirdeye", "thirdleg",
		"threesome", "threeway", "throating", "thumbzilla", "thundercunt", "tied up", "tig ol bitties", "tig old bitties", "tight white", "timber nigger",
		"timber niggers", "timbernigger", "tit", "titbitnipply", "titfuck", "titfucker", "titfuckin", "titi", "titjob", "titlicker",
		"titlover", "tits", "titt", "tittie", "tittie5", "tittiefucker", "titties", "tittis", "titty", "tittyfuck",
		"tittyfucker", "tittys", "tittywank", "titwank", "tity", "to murder", "tongethruster", "tongue in a", "tongueina", "tonguethrust",
		"tonguetramp", "toots", "topless", "tortur", "torture", "tosser", "towel head", "towel heads", "towelhead", "trailertrash",
		"trannie", "tranny", "transsexual", "transvestite", "tribadism", "trisexual", "trois", "trots", "tub girl", "tubgirl",
		"tuckahoe", "tunneloflove", "turd burgler", "turnon", "tush", "tushy", "tw4t", "twat", "twathead", "twatlips",
		"twats", "twatty", "twatwaffle", "twink", "twinkie", "two girls one cup", "twobitwhore", "twunt", "twunter", "udge packer",
		"ukrop", "unclefucker", "unfuckable", "upskirt", "uptheass", "upthebutt", "urethra play", "urethraplay", "urophilia", "usama",
		"ussys", "uzi", "v a g i n a", "v14gra", "v1gra", "v4gra", "va-j-j", "va1jina", "vag", "vag1na",
		"vagiina", "vaj1na", "vajina", "valium", "venus mound", "vgra", "vibr", "vibrater", "vibrator", "vigra",
		"violet wand", "virginbreaker", "vittu", "vixen", "vjayjay", "vorarephilia", "voyeurweb", "voyuer", "vullva", "vulva",
		"w00se", "w0p", "wab", "wang", "wank", "wanker", "wanking", "wanky", "waysted", "wazoo",
		"weenie", "weewee", "weiner", "welcher", "wench", "wet dream", "wetb", "wetback", "wetbacks", "wetdream",
		"wetspot", "wh00r", "wh0re", "wh0reface", "whacker", "whash", "whigger", "whiggers", "whiskeydick", "whiskydick",
		"whit", "white power", "white trash", "whitenigger", "whitepower", "whitetrash", "whitey", "whiteys", "whities", "whoar",
		"whop", "whoralicious", "whore", "whorealicious", "whorebag", "whored", "whoreface", "whorefucker", "whorehopper", "whorehouse",
		"whores", "whoring", "wichser", "wigga", "wiggas", "wigger", "wiggers", "willie", "willies", "williewanker",
		"willy", "wog", "wogs", "woose", "wop", "worldsex", "wrapping men", "wrinkled starfish", "wtf", "wuss",
		"wuzzie", "x-rated", "x-rated2g1c", "xkwe", "xrated", "xtc", "xx", "xxx", "xxxxxx", "yank",
		"yaoi", "yarpie", "yarpies", "yed", "yellow showers", "yellowman", "yellowshowers", "yid", "yids", "yiffy",
		"yobbo", "yourboobs", "yourpenis", "yourtits", "yury", "zabourah", "zigabo", "zigabos", "zipperhead", "zipperheads",
		"zoophile", "zoophilia", "\ud83d\udd95"
	};

	public static string ProfanityWordsJoined = string.Join('|', ProfanityWords);

	public static Vector3 WrapEulerAngles(Vector3 eulerAngles)
	{
		eulerAngles.x %= 360f;
		if (eulerAngles.x > 180f)
		{
			eulerAngles.x -= 360f;
		}
		if (eulerAngles.x < -180f)
		{
			eulerAngles.x += 360f;
		}
		eulerAngles.y %= 360f;
		if (eulerAngles.y > 180f)
		{
			eulerAngles.y -= 360f;
		}
		if (eulerAngles.y < -180f)
		{
			eulerAngles.y += 360f;
		}
		eulerAngles.z %= 360f;
		if (eulerAngles.z > 180f)
		{
			eulerAngles.z -= 360f;
		}
		if (eulerAngles.z < -180f)
		{
			eulerAngles.z += 360f;
		}
		return eulerAngles;
	}

	public static TemplateContainer InstantiateVisualTreeAsset(VisualTreeAsset asset, Position position = Position.Absolute)
	{
		TemplateContainer templateContainer = asset.Instantiate();
		templateContainer.style.display = DisplayStyle.Flex;
		templateContainer.style.position = position;
		templateContainer.style.left = 0f;
		templateContainer.style.top = 0f;
		templateContainer.style.right = 0f;
		templateContainer.style.bottom = 0f;
		templateContainer.pickingMode = PickingMode.Ignore;
		return templateContainer;
	}

	public static Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Vector3 angles)
	{
		Vector3 vector = point - pivot;
		vector = Quaternion.Euler(angles) * vector;
		point = vector + pivot;
		return point;
	}

	public static Vector3 Vector2Clamp(Vector2 value, Vector2 min, Vector2 max)
	{
		return new Vector3(Mathf.Clamp(value.x, min.x, max.x), Mathf.Clamp(value.y, min.y, max.y));
	}

	public static Vector3 Vector3Clamp(Vector3 value, Vector3 min, Vector3 max)
	{
		return new Vector3(Mathf.Clamp(value.x, min.x, max.x), Mathf.Clamp(value.y, min.y, max.y), Mathf.Clamp(value.z, min.z, max.z));
	}

	public static Vector3 Vector3Abs(Vector3 value)
	{
		return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
	}

	public static Vector3 Vector3Flip(Vector3 value)
	{
		return new Vector3((value.x > 0f) ? (0f - value.x) : Mathf.Abs(value.x), (value.y > 0f) ? (0f - value.y) : Mathf.Abs(value.y), (value.z > 0f) ? (0f - value.z) : Mathf.Abs(value.z));
	}

	public static Vector3 Vector3Lerp3(Vector3 a, Vector3 b, Vector3 c, float t)
	{
		if (t <= 0f)
		{
			return Vector3.Lerp(a, b, t + 1f);
		}
		return Vector3.Lerp(b, c, t);
	}

	public static Vector3 Vector3Slerp3(Vector3 a, Vector3 b, Vector3 c, float t)
	{
		if (t <= 0f)
		{
			return Vector3.Slerp(a, b, t + 1f);
		}
		return Vector3.Slerp(b, c, t);
	}

	public static float Map(float value, float from1, float to1, float from2, float to2)
	{
		return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
	}

	public static float GameUnitsToMetric(float value)
	{
		return value * 3.6f;
	}

	public static float GameUnitsToImperial(float value)
	{
		return value * 2.2369363f;
	}

	public static async Task<string> GetPublicIpAddress()
	{
		UnityWebRequest uwr = UnityWebRequest.Get("https://ipv4.icanhazip.com/");
		await uwr.SendWebRequest();
		if (uwr.result == UnityWebRequest.Result.Success)
		{
			return Regex.Replace(uwr.downloadHandler.text, "\\t|\\n|\\r", "");
		}
		return null;
	}

	public static float GetCollisionForce(Collision collision)
	{
		if (collision == null)
		{
			return 0f;
		}
		float result = 0f;
		if (collision.contacts.Length != 0)
		{
			result = Vector3.Dot(collision.contacts[0].normal, collision.relativeVelocity.normalized) * collision.relativeVelocity.magnitude;
		}
		return result;
	}

	public static string CountryCodeToName(string code)
	{
		if (CountryDictionary.ContainsKey(code))
		{
			return CountryDictionary[code];
		}
		return null;
	}

	public static string CountryNameToCode(string name)
	{
		foreach (KeyValuePair<string, string> item in CountryDictionary)
		{
			if (item.Value == name)
			{
				return item.Key;
			}
		}
		return null;
	}

	public static string FilterStringNotLetters(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		return Regex.Replace(text, "[^\\p{L}]+", "");
	}

	public static string FilterStringSpecialCharacters(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		return Regex.Replace(text, "[^\\p{L}0-9\t\n ,./<>?;:\"'`!@#$%^&*()\\[\\]{}_+=|\\-~]+", "");
	}

	public static string FilterStringProfanity(string text, bool replaceWithStars = false)
	{
		if (string.IsNullOrEmpty(text))
		{
			return text;
		}
		Regex regex = new Regex("\\b(" + ProfanityWordsJoined + ")\\b", RegexOptions.IgnoreCase);
		if (replaceWithStars)
		{
			foreach (Match item in regex.Matches(text))
			{
				text = text.Replace(item.Value, new string('*', item.Value.Length));
			}
		}
		else
		{
			text = regex.Replace(text, "");
		}
		return text;
	}

	public static void SetRigidbodyCollisionDetectionMode(Rigidbody rigidbody, CollisionDetectionMode mode)
	{
		if (!(rigidbody == null) && rigidbody.collisionDetectionMode != mode)
		{
			bool isKinematic = rigidbody.isKinematic;
			Vector3 linearVelocity = rigidbody.linearVelocity;
			Vector3 angularVelocity = rigidbody.angularVelocity;
			rigidbody.collisionDetectionMode = mode;
			rigidbody.isKinematic = true;
			rigidbody.isKinematic = false;
			rigidbody.isKinematic = isKinematic;
			rigidbody.linearVelocity = linearVelocity;
			rigidbody.angularVelocity = angularVelocity;
		}
	}

	public static int GetMainDisplayIndex()
	{
		List<DisplayInfo> list = new List<DisplayInfo>();
		Screen.GetDisplayLayout(list);
		return list.FindIndex((DisplayInfo x) => x.name == Screen.mainWindowDisplayInfo.name);
	}

	public static string GetDisplayStringFromIndex(int index)
	{
		List<DisplayInfo> list = new List<DisplayInfo>();
		Screen.GetDisplayLayout(list);
		if (index >= list.Count)
		{
			return list[0].name;
		}
		return list[index].name;
	}

	public static string GetResolutionStringFromIndex(int index)
	{
		return string.Format("{0}x{1} {2}Hz", Screen.resolutions[index].width, Screen.resolutions[index].height, Screen.resolutions[index].refreshRateRatio.value.ToString("F0"));
	}

	public static int GetDefaultResolutionIndex(DisplayInfo displayInfo)
	{
		int i;
		for (i = 0; i < Screen.resolutions.Length && (Screen.resolutions[i].width != displayInfo.width || Screen.resolutions[i].height != displayInfo.height || Screen.resolutions[i].refreshRateRatio.value != displayInfo.refreshRate.value); i++)
		{
		}
		if (i != Screen.resolutions.Length)
		{
			return i;
		}
		return 0;
	}

	public static string GetHumanizedInteractionFromInteraction(string interaction, bool isPressable, bool isHoldable)
	{
		if (isPressable)
		{
			if (interaction.Contains("Press(behavior=0)"))
			{
				return "PRESS";
			}
			if (interaction.Contains("Press(behavior=1)"))
			{
				return "RELEASE";
			}
			if (interaction.Contains("DoublePress"))
			{
				return "DOUBLE PRESS";
			}
			if (interaction.Contains("Hold"))
			{
				return "HOLD";
			}
			return "PRESS";
		}
		if (isHoldable)
		{
			if (interaction.Contains("Toggle"))
			{
				return "TOGGLE";
			}
			return "CONTINUOUS";
		}
		return null;
	}

	public static string GetInteractionFromHumanizedInteraction(string humanizedInteraction)
	{
		return humanizedInteraction switch
		{
			"PRESS" => "Press(behavior=0)", 
			"RELEASE" => "Press(behavior=1)", 
			"DOUBLE PRESS" => "DoublePress", 
			"HOLD" => "Hold", 
			"TOGGLE" => "Toggle", 
			_ => "", 
		};
	}

	public static List<VisualElement> GetVisualElementChildrenRecursive(VisualElement element)
	{
		List<VisualElement> list = new List<VisualElement>();
		foreach (VisualElement item in element.hierarchy.Children())
		{
			list.Add(item);
			list.AddRange(GetVisualElementChildrenRecursive(item));
		}
		return list;
	}

	public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(sourceDir);
		if (!directoryInfo.Exists)
		{
			throw new DirectoryNotFoundException("Source directory not found: " + directoryInfo.FullName);
		}
		DirectoryInfo[] directories = directoryInfo.GetDirectories();
		Directory.CreateDirectory(destinationDir);
		FileInfo[] files = directoryInfo.GetFiles();
		foreach (FileInfo fileInfo in files)
		{
			string destFileName = Path.Combine(destinationDir, fileInfo.Name);
			fileInfo.CopyTo(destFileName, overwrite: true);
		}
		if (recursive)
		{
			DirectoryInfo[] array = directories;
			foreach (DirectoryInfo directoryInfo2 in array)
			{
				string destinationDir2 = Path.Combine(destinationDir, directoryInfo2.Name);
				CopyDirectory(directoryInfo2.FullName, destinationDir2, recursive: true);
			}
		}
	}

	public static string GetConnectionRejectionMessage(ConnectionRejectionCode code)
	{
		return code switch
		{
			ConnectionRejectionCode.InvalidSocketId => "Invalid Socket ID", 
			ConnectionRejectionCode.InvalidSteamId => "Invalid Steam ID", 
			ConnectionRejectionCode.ServerFull => "Server full", 
			ConnectionRejectionCode.TimedOut => "Timed out", 
			ConnectionRejectionCode.Banned => "Banned", 
			ConnectionRejectionCode.MissingPassword => "Missing password", 
			ConnectionRejectionCode.InvalidPassword => "Invalid password", 
			ConnectionRejectionCode.MissingMods => "Missing mods", 
			_ => "Server unreachable", 
		};
	}

	public static string GetDisconnectionMessage(DisconnectionCode code)
	{
		return code switch
		{
			DisconnectionCode.Kicked => "Kicked", 
			DisconnectionCode.Banned => "Banned", 
			_ => "Disconnected", 
		};
	}

	public static string GetCommandLineArgument(string name, string[] args = null)
	{
		if (args == null)
		{
			args = Environment.GetCommandLineArgs();
		}
		for (int i = 0; i < args.Length; i++)
		{
			if (args[i] == (name ?? ""))
			{
				if (i + 1 >= args.Length)
				{
					return null;
				}
				return args[i + 1];
			}
		}
		return null;
	}
}
