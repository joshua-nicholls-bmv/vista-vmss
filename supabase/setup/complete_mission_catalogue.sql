-- VISTA project dlqfpbqenqumdlogfrxo ONLY. Run this complete file in its SQL Editor.
-- Requires the initial VISTA schema, pilot-display migration and initial fleet setup.
-- One transaction. Newly seeded options are enabled. Repeating this file preserves changes.
-- Distances are NM; unspecified altitudes/speeds are NULL, not zero.
begin;
create table if not exists vista_private.catalogue_installations (
  code text primary key, installed_at timestamptz not null default now()
);
revoke all on vista_private.catalogue_installations from public, anon, authenticated;
do $setup$
declare
  roster constant jsonb := $roster${
  "bases": [
    {
      "icao": "EGXC",
      "name": "RAF Coningsby"
    },
    {
      "icao": "EGQS",
      "name": "RAF Lossiemouth"
    },
    {
      "icao": "EGVN",
      "name": "RAF Brize Norton"
    },
    {
      "icao": "EGYM",
      "name": "RAF Marham"
    },
    {
      "icao": "EGOS",
      "name": "RAF Shawbury"
    },
    {
      "icao": "EGOV",
      "name": "RAF Valley"
    },
    {
      "icao": "EGEC",
      "name": "Campbeltown / former RAF Machrihanish"
    },
    {
      "icao": "EGHQ",
      "name": "Newquay / former RAF St Mawgan"
    },
    {
      "icao": "EGQL",
      "name": "MOD Leuchars"
    },
    {
      "icao": "EGXE",
      "name": "RAF Leeming"
    },
    {
      "icao": "EGUW",
      "name": "AAC Wattisham"
    },
    {
      "icao": "EGVO",
      "name": "RAF Odiham"
    },
    {
      "icao": "EGUB",
      "name": "RAF Benson"
    },
    {
      "icao": "EGXW",
      "name": "RAF Waddington"
    },
    {
      "icao": "EGWC",
      "name": "RAF Cosford"
    },
    {
      "icao": "EGPL",
      "name": "Benbecula Airport"
    },
    {
      "icao": "LCRA",
      "name": "RAF Akrotiri"
    },
    {
      "icao": "ETAR",
      "name": "Ramstein Air Base"
    },
    {
      "icao": "ENGM",
      "name": "Oslo Gardermoen"
    },
    {
      "icao": "LXGB",
      "name": "RAF Gibraltar"
    },
    {
      "icao": "EPRZ",
      "name": "Rzeszow-Jasionka"
    }
  ],
  "families": [
    {
      "code": "TY-BVR",
      "title": "BVR Training",
      "types": [
        "TYPHOON-FGR4",
        "TYPHOON-T3"
      ],
      "departure": [
        "EGXC",
        "EGQS"
      ],
      "arrival": [
        "EGXC",
        "EGQS"
      ]
    },
    {
      "code": "TY-STRIKE",
      "title": "Practice Strike",
      "types": [
        "TYPHOON-FGR4",
        "TYPHOON-T3"
      ],
      "departure": [
        "EGXC",
        "EGQS"
      ],
      "arrival": [
        "EGXC",
        "EGQS"
      ]
    },
    {
      "code": "TY-LFA7",
      "title": "Low-Level Navigation LFA7",
      "types": [
        "TYPHOON-FGR4",
        "TYPHOON-T3"
      ],
      "departure": [
        "EGXC",
        "EGQS"
      ],
      "arrival": [
        "EGXC",
        "EGQS"
      ]
    },
    {
      "code": "TY-LFA17",
      "title": "Low-Level Navigation LFA17",
      "types": [
        "TYPHOON-FGR4",
        "TYPHOON-T3"
      ],
      "departure": [
        "EGXC",
        "EGQS"
      ],
      "arrival": [
        "EGXC",
        "EGQS"
      ]
    },
    {
      "code": "TY-AAR",
      "title": "Air-to-Air Refuelling",
      "types": [
        "TYPHOON-FGR4",
        "TYPHOON-T3"
      ],
      "departure": [
        "EGXC",
        "EGQS"
      ],
      "arrival": [
        "EGXC",
        "EGQS"
      ]
    },
    {
      "code": "TY-DISPERSAL",
      "title": "Dispersal Exercise",
      "types": [
        "TYPHOON-FGR4",
        "TYPHOON-T3"
      ],
      "departure": [
        "EGXC",
        "EGQS"
      ],
      "arrival": []
    },
    {
      "code": "AMF-LOGISTICS",
      "title": "UK Logistics Shuttle",
      "types": [
        "ATLAS"
      ],
      "departure": [
        "EGVN"
      ],
      "arrival": []
    },
    {
      "code": "AMF-FIGHTER",
      "title": "Fighter Fleet Support",
      "types": [
        "ATLAS"
      ],
      "departure": [
        "EGVN"
      ],
      "arrival": []
    },
    {
      "code": "AMF-PERSONNEL",
      "title": "Personnel Transport",
      "types": [
        "ATLAS"
      ],
      "departure": [
        "EGVN"
      ],
      "arrival": []
    },
    {
      "code": "AMF-AIRDROP",
      "title": "Airdrop Practice",
      "types": [
        "ATLAS"
      ],
      "departure": [
        "EGVN"
      ],
      "arrival": [
        "EGVN"
      ]
    },
    {
      "code": "AMF-LFA7",
      "title": "Low-Level Navigation LFA7",
      "types": [
        "ATLAS"
      ],
      "departure": [
        "EGVN"
      ],
      "arrival": [
        "EGVN"
      ]
    },
    {
      "code": "AMF-LFA17",
      "title": "Low-Level Navigation LFA17",
      "types": [
        "ATLAS"
      ],
      "departure": [
        "EGVN"
      ],
      "arrival": [
        "EGVN"
      ]
    }
  ],
  "elements": [
    {
      "code": "WASH-NORTH",
      "title": "Wash North ATA",
      "kind": "bvr",
      "points": [
        {
          "identifier": "WASHN",
          "latitude": 53.3378,
          "longitude": 1.149,
          "altitude_ft": 14000,
          "speed_kts": null,
          "instructions": "Enter at FL140 (standard pressure). Area limits supplied by user: FL050-FL245; this point is an area reference, not its boundary."
        }
      ],
      "metadata": {
        "entry_flight_level": 140,
        "lower_flight_level": 50,
        "upper_flight_level": 245,
        "reference": "flight_level"
      }
    },
    {
      "code": "STRIKE-1",
      "title": "Clee Hill Radar",
      "kind": "practice_strike",
      "points": [
        {
          "identifier": "S1ENTRY",
          "latitude": 52.531476,
          "longitude": -2.597386,
          "altitude_ft": 4000,
          "speed_kts": 350,
          "instructions": "Entry 8 NM due north of the supplied virtual target; 4000 ft, 350 kt. Must precede target."
        },
        {
          "identifier": "S1TARGET",
          "latitude": 52.39833,
          "longitude": -2.597386,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "entry_offset_nm": 8,
        "entry_bearing_true": 0,
        "simulator_scenario": true
      }
    },
    {
      "code": "STRIKE-2",
      "title": "Howden Dam",
      "kind": "practice_strike",
      "points": [
        {
          "identifier": "S2ENTRY",
          "latitude": 53.561772,
          "longitude": -1.746103,
          "altitude_ft": 4000,
          "speed_kts": 350,
          "instructions": "Entry 8 NM due north of the supplied virtual target; 4000 ft, 350 kt. Must precede target."
        },
        {
          "identifier": "S2TARGET",
          "latitude": 53.428649,
          "longitude": -1.746103,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "entry_offset_nm": 8,
        "entry_bearing_true": 0,
        "simulator_scenario": true
      }
    },
    {
      "code": "STRIKE-3",
      "title": "RAF Spadeham",
      "kind": "practice_strike",
      "points": [
        {
          "identifier": "S3ENTRY",
          "latitude": 55.157957,
          "longitude": -2.603155,
          "altitude_ft": 4000,
          "speed_kts": 350,
          "instructions": "Entry 8 NM due north of the supplied virtual target; 4000 ft, 350 kt. Must precede target."
        },
        {
          "identifier": "S3TARGET",
          "latitude": 55.024869,
          "longitude": -2.603155,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "entry_offset_nm": 8,
        "entry_bearing_true": 0,
        "simulator_scenario": true
      }
    },
    {
      "code": "STRIKE-4",
      "title": "Gairlochy Bridge / Loch Lochy",
      "kind": "practice_strike",
      "points": [
        {
          "identifier": "S4ENTRY",
          "latitude": 57.046071,
          "longitude": -4.997318,
          "altitude_ft": 4000,
          "speed_kts": 350,
          "instructions": "Entry 8 NM due north of the supplied virtual target; 4000 ft, 350 kt. Must precede target."
        },
        {
          "identifier": "S4TARGET",
          "latitude": 56.913024,
          "longitude": -4.997318,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "entry_offset_nm": 8,
        "entry_bearing_true": 0,
        "simulator_scenario": true
      }
    },
    {
      "code": "STRIKE-5",
      "title": "Sellafield Nuclear Plant",
      "kind": "practice_strike",
      "points": [
        {
          "identifier": "S5ENTRY",
          "latitude": 54.554759,
          "longitude": -3.499315,
          "altitude_ft": 4000,
          "speed_kts": 350,
          "instructions": "Entry 8 NM due north of the supplied virtual target; 4000 ft, 350 kt. Must precede target."
        },
        {
          "identifier": "S5TARGET",
          "latitude": 54.421658,
          "longitude": -3.499315,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "entry_offset_nm": 8,
        "entry_bearing_true": 0,
        "simulator_scenario": true
      }
    },
    {
      "code": "LFA7",
      "title": "LFA7 - North Wales",
      "kind": "low_level",
      "points": [
        {
          "identifier": "LFA7",
          "latitude": 52.512696,
          "longitude": -3.422424,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "point_role": "area_reference"
      }
    },
    {
      "code": "LFA17",
      "title": "LFA17 - Lake District",
      "kind": "low_level",
      "points": [
        {
          "identifier": "LFA17",
          "latitude": 54.160691,
          "longitude": -3.002588,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "point_role": "area_reference"
      }
    },
    {
      "code": "AARA-8",
      "title": "AARA 8",
      "kind": "aar",
      "points": [
        {
          "identifier": "AARA8",
          "latitude": 53.338006,
          "longitude": 1.033759,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "point_role": "area_reference"
      }
    },
    {
      "code": "AARA-12",
      "title": "AARA 12",
      "kind": "aar",
      "points": [
        {
          "identifier": "AARA12",
          "latitude": 51.048203,
          "longitude": -4.952413,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "point_role": "area_reference"
      }
    },
    {
      "code": "AARA-13",
      "title": "AARA 13",
      "kind": "aar",
      "points": [
        {
          "identifier": "AARA13",
          "latitude": 54.003066,
          "longitude": -3.752748,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "point_role": "area_reference"
      }
    },
    {
      "code": "DROP-DALTON",
      "title": "Dalton Barracks",
      "kind": "airdrop",
      "points": [
        {
          "identifier": "DALTON-IN",
          "latitude": 51.757172,
          "longitude": -1.316652,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        },
        {
          "identifier": "DALTON-DROP",
          "latitude": 51.69059,
          "longitude": -1.316652,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        },
        {
          "identifier": "DALTON-OUT",
          "latitude": 51.624008,
          "longitude": -1.316652,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "entry_offset_nm": 4,
        "exit_offset_nm": 4,
        "entry_bearing_true": 0,
        "exit_bearing_true": 180
      }
    },
    {
      "code": "DROP-NETHERAVON",
      "title": "Netheravon / Salisbury Plain",
      "kind": "airdrop",
      "points": [
        {
          "identifier": "NETHERAVON-IN",
          "latitude": 51.314859,
          "longitude": -1.754278,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        },
        {
          "identifier": "NETHERAVON-DROP",
          "latitude": 51.248272,
          "longitude": -1.754278,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        },
        {
          "identifier": "NETHERAVON-OUT",
          "latitude": 51.181685,
          "longitude": -1.754278,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "entry_offset_nm": 4,
        "exit_offset_nm": 4,
        "entry_bearing_true": 0,
        "exit_bearing_true": 180
      }
    },
    {
      "code": "DROP-PEMBREY",
      "title": "Pembrey Sands",
      "kind": "airdrop",
      "points": [
        {
          "identifier": "PEMBREY-IN",
          "latitude": 51.661129,
          "longitude": -4.28969,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        },
        {
          "identifier": "PEMBREY-DROP",
          "latitude": 51.708234,
          "longitude": -4.365392,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        },
        {
          "identifier": "PEMBREY-OUT",
          "latitude": 51.75529,
          "longitude": -4.441252,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "entry_offset_nm": 4,
        "exit_offset_nm": 4,
        "entry_bearing_true": 135,
        "exit_bearing_true": 315
      }
    },
    {
      "code": "LICHFIELD-OUT",
      "title": "Lichfield Corridor - outbound E to W",
      "kind": "transit",
      "points": [
        {
          "identifier": "LICHOUT1",
          "latitude": 52.762535,
          "longitude": -1.312405,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        },
        {
          "identifier": "LICHOUT2",
          "latitude": 52.705118,
          "longitude": -1.670727,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        },
        {
          "identifier": "LICHOUT3",
          "latitude": 52.682981,
          "longitude": -1.831615,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        },
        {
          "identifier": "LICHOUT4",
          "latitude": 52.632646,
          "longitude": -2.174131,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "placement": "before_tasks",
        "direction": "east_to_west"
      }
    },
    {
      "code": "LICHFIELD-RETURN",
      "title": "Lichfield Corridor - return W to E",
      "kind": "transit",
      "points": [
        {
          "identifier": "LICHRET1",
          "latitude": 52.649289,
          "longitude": -2.174131,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        },
        {
          "identifier": "LICHRET2",
          "latitude": 52.699624,
          "longitude": -1.831615,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        },
        {
          "identifier": "LICHRET3",
          "latitude": 52.721761,
          "longitude": -1.670727,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        },
        {
          "identifier": "LICHRET4",
          "latitude": 52.779177,
          "longitude": -1.312405,
          "altitude_ft": null,
          "speed_kts": null,
          "instructions": "Pilot discretion for altitude and speed unless otherwise specified."
        }
      ],
      "metadata": {
        "placement": "after_tasks",
        "direction": "west_to_east",
        "north_offset_nm": 1
      }
    }
  ],
  "options": [
    {
      "code": "BVR-WASH-NORTH",
      "family": "TY-BVR",
      "title": "Wash North ATA",
      "element": "WASH-NORTH",
      "destination": null
    },
    {
      "code": "TY-STRIKE-1",
      "family": "TY-STRIKE",
      "title": "Clee Hill Radar",
      "element": "STRIKE-1",
      "destination": null
    },
    {
      "code": "TY-STRIKE-2",
      "family": "TY-STRIKE",
      "title": "Howden Dam",
      "element": "STRIKE-2",
      "destination": null
    },
    {
      "code": "TY-STRIKE-3",
      "family": "TY-STRIKE",
      "title": "RAF Spadeham",
      "element": "STRIKE-3",
      "destination": null
    },
    {
      "code": "TY-STRIKE-4",
      "family": "TY-STRIKE",
      "title": "Gairlochy Bridge / Loch Lochy",
      "element": "STRIKE-4",
      "destination": null
    },
    {
      "code": "TY-STRIKE-5",
      "family": "TY-STRIKE",
      "title": "Sellafield Nuclear Plant",
      "element": "STRIKE-5",
      "destination": null
    },
    {
      "code": "TY-LFA7-AREA",
      "family": "TY-LFA7",
      "title": "LFA7 - North Wales",
      "element": "LFA7",
      "destination": null
    },
    {
      "code": "AMF-LFA7-AREA",
      "family": "AMF-LFA7",
      "title": "LFA7 - North Wales",
      "element": "LFA7",
      "destination": null
    },
    {
      "code": "TY-LFA17-AREA",
      "family": "TY-LFA17",
      "title": "LFA17 - Lake District",
      "element": "LFA17",
      "destination": null
    },
    {
      "code": "AMF-LFA17-AREA",
      "family": "AMF-LFA17",
      "title": "LFA17 - Lake District",
      "element": "LFA17",
      "destination": null
    },
    {
      "code": "TY-AARA-8",
      "family": "TY-AAR",
      "title": "AARA 8",
      "element": "AARA-8",
      "destination": null
    },
    {
      "code": "TY-AARA-12",
      "family": "TY-AAR",
      "title": "AARA 12",
      "element": "AARA-12",
      "destination": null
    },
    {
      "code": "TY-AARA-13",
      "family": "TY-AAR",
      "title": "AARA 13",
      "element": "AARA-13",
      "destination": null
    },
    {
      "code": "DISP-EGYM",
      "family": "TY-DISPERSAL",
      "title": "RAF Marham",
      "element": null,
      "destination": "EGYM"
    },
    {
      "code": "DISP-EGOS",
      "family": "TY-DISPERSAL",
      "title": "RAF Shawbury",
      "element": null,
      "destination": "EGOS"
    },
    {
      "code": "DISP-EGOV",
      "family": "TY-DISPERSAL",
      "title": "RAF Valley",
      "element": null,
      "destination": "EGOV"
    },
    {
      "code": "DISP-EGEC",
      "family": "TY-DISPERSAL",
      "title": "Campbeltown / former RAF Machrihanish",
      "element": null,
      "destination": "EGEC"
    },
    {
      "code": "DISP-EGHQ",
      "family": "TY-DISPERSAL",
      "title": "Newquay / former RAF St Mawgan",
      "element": null,
      "destination": "EGHQ"
    },
    {
      "code": "DISP-EGQL",
      "family": "TY-DISPERSAL",
      "title": "MOD Leuchars",
      "element": null,
      "destination": "EGQL"
    },
    {
      "code": "DISP-EGXE",
      "family": "TY-DISPERSAL",
      "title": "RAF Leeming",
      "element": null,
      "destination": "EGXE"
    },
    {
      "code": "DISP-EGUW",
      "family": "TY-DISPERSAL",
      "title": "AAC Wattisham",
      "element": null,
      "destination": "EGUW"
    },
    {
      "code": "LOG-EGYM",
      "family": "AMF-LOGISTICS",
      "title": "RAF Marham",
      "element": null,
      "destination": "EGYM"
    },
    {
      "code": "LOG-EGXC",
      "family": "AMF-LOGISTICS",
      "title": "RAF Coningsby",
      "element": null,
      "destination": "EGXC"
    },
    {
      "code": "LOG-EGQS",
      "family": "AMF-LOGISTICS",
      "title": "RAF Lossiemouth",
      "element": null,
      "destination": "EGQS"
    },
    {
      "code": "LOG-EGVO",
      "family": "AMF-LOGISTICS",
      "title": "RAF Odiham",
      "element": null,
      "destination": "EGVO"
    },
    {
      "code": "LOG-EGUB",
      "family": "AMF-LOGISTICS",
      "title": "RAF Benson",
      "element": null,
      "destination": "EGUB"
    },
    {
      "code": "LOG-EGXW",
      "family": "AMF-LOGISTICS",
      "title": "RAF Waddington",
      "element": null,
      "destination": "EGXW"
    },
    {
      "code": "LOG-EGWC",
      "family": "AMF-LOGISTICS",
      "title": "RAF Cosford",
      "element": null,
      "destination": "EGWC"
    },
    {
      "code": "LOG-EGXE",
      "family": "AMF-LOGISTICS",
      "title": "RAF Leeming",
      "element": null,
      "destination": "EGXE"
    },
    {
      "code": "LOG-EGOV",
      "family": "AMF-LOGISTICS",
      "title": "RAF Valley",
      "element": null,
      "destination": "EGOV"
    },
    {
      "code": "LOG-EGPL",
      "family": "AMF-LOGISTICS",
      "title": "Benbecula Airport",
      "element": null,
      "destination": "EGPL"
    },
    {
      "code": "SUPPORT-EGXC",
      "family": "AMF-FIGHTER",
      "title": "RAF Coningsby",
      "element": null,
      "destination": "EGXC"
    },
    {
      "code": "SUPPORT-EGQS",
      "family": "AMF-FIGHTER",
      "title": "RAF Lossiemouth",
      "element": null,
      "destination": "EGQS"
    },
    {
      "code": "PAX-LCRA",
      "family": "AMF-PERSONNEL",
      "title": "RAF Akrotiri",
      "element": null,
      "destination": "LCRA"
    },
    {
      "code": "PAX-ETAR",
      "family": "AMF-PERSONNEL",
      "title": "Ramstein Air Base",
      "element": null,
      "destination": "ETAR"
    },
    {
      "code": "PAX-ENGM",
      "family": "AMF-PERSONNEL",
      "title": "Oslo Gardermoen",
      "element": null,
      "destination": "ENGM"
    },
    {
      "code": "PAX-LXGB",
      "family": "AMF-PERSONNEL",
      "title": "RAF Gibraltar",
      "element": null,
      "destination": "LXGB"
    },
    {
      "code": "PAX-EPRZ",
      "family": "AMF-PERSONNEL",
      "title": "Rzeszow-Jasionka",
      "element": null,
      "destination": "EPRZ"
    },
    {
      "code": "AMF-DROP-DALTON",
      "family": "AMF-AIRDROP",
      "title": "Dalton Barracks",
      "element": "DROP-DALTON",
      "destination": null
    },
    {
      "code": "AMF-DROP-NETHERAVON",
      "family": "AMF-AIRDROP",
      "title": "Netheravon / Salisbury Plain",
      "element": "DROP-NETHERAVON",
      "destination": null
    },
    {
      "code": "AMF-DROP-PEMBREY",
      "family": "AMF-AIRDROP",
      "title": "Pembrey Sands",
      "element": "DROP-PEMBREY",
      "destination": null
    }
  ],
  "payloads": [
    {
      "code": "CARGO-01",
      "title": "Typhoon Maintenance Pack",
      "description": "Aircraft spares, replacement panels, filters and maintenance tooling",
      "kg": 3200,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGXC",
        "EGQS"
      ],
      "families": [
        "AMF-LOGISTICS",
        "AMF-FIGHTER"
      ]
    },
    {
      "code": "CARGO-02",
      "title": "Typhoon Avionics Support",
      "description": "Avionics modules, diagnostic equipment and electrical spares",
      "kg": 2400,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGXC",
        "EGQS"
      ],
      "families": [
        "AMF-LOGISTICS",
        "AMF-FIGHTER"
      ]
    },
    {
      "code": "CARGO-03",
      "title": "Air-to-Air Stores Delivery",
      "description": "Simulated A2A weapons consignment and support equipment",
      "kg": 4500,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGYM",
        "EGXC",
        "EGQS"
      ],
      "families": [
        "AMF-LOGISTICS",
        "AMF-FIGHTER"
      ]
    },
    {
      "code": "CARGO-04",
      "title": "Air-to-Ground Stores Delivery",
      "description": "Simulated A2G weapons consignment and support equipment",
      "kg": 6000,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGYM",
        "EGXC",
        "EGQS"
      ],
      "families": [
        "AMF-LOGISTICS",
        "AMF-FIGHTER"
      ]
    },
    {
      "code": "CARGO-05",
      "title": "Fighter Exercise Support",
      "description": "Ground equipment, servicing kits, spare wheels and deployment stores",
      "kg": 7500,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGYM",
        "EGXC",
        "EGQS"
      ],
      "families": [
        "AMF-LOGISTICS",
        "AMF-FIGHTER"
      ]
    },
    {
      "code": "CARGO-06",
      "title": "Lightning Maintenance Pack",
      "description": "F-35 spares, diagnostic equipment and maintenance tooling",
      "kg": 3800,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGYM"
      ],
      "families": [
        "AMF-LOGISTICS"
      ]
    },
    {
      "code": "CARGO-07",
      "title": "Helicopter Mechanical Spares",
      "description": "Rotor-system spares, transmission components and hydraulics",
      "kg": 4200,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGVO",
        "EGUB"
      ],
      "families": [
        "AMF-LOGISTICS"
      ]
    },
    {
      "code": "CARGO-08",
      "title": "Helicopter Winch Support",
      "description": "Winch assemblies, rescue equipment and inspection tooling",
      "kg": 1800,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGVO",
        "EGUB"
      ],
      "families": [
        "AMF-LOGISTICS"
      ]
    },
    {
      "code": "CARGO-09",
      "title": "Helicopter Engine Support",
      "description": "Engine modules, servicing equipment and specialist tools",
      "kg": 5500,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGVO",
        "EGUB"
      ],
      "families": [
        "AMF-LOGISTICS"
      ]
    },
    {
      "code": "CARGO-10",
      "title": "Helicopter Exercise Pack",
      "description": "Maintenance shelters, ground equipment, spares and crew support stores",
      "kg": 8000,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGVO",
        "EGUB"
      ],
      "families": [
        "AMF-LOGISTICS"
      ]
    },
    {
      "code": "CARGO-11",
      "title": "Training Aircraft Spares",
      "description": "Replacement components, tyres, brakes and servicing consumables",
      "kg": 2600,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGOV",
        "EGXE"
      ],
      "families": [
        "AMF-LOGISTICS"
      ]
    },
    {
      "code": "CARGO-12",
      "title": "Flying Training Support",
      "description": "Training equipment, simulator components and classroom technology",
      "kg": 3400,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGOV",
        "EGXE"
      ],
      "families": [
        "AMF-LOGISTICS"
      ]
    },
    {
      "code": "CARGO-13",
      "title": "Engineering Training Equipment",
      "description": "Workshop machinery, training assemblies, tool cabinets and test benches",
      "kg": 6500,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGWC"
      ],
      "families": [
        "AMF-LOGISTICS"
      ]
    },
    {
      "code": "CARGO-14",
      "title": "Training Station Resupply",
      "description": "Technical stores, protective equipment and training supplies",
      "kg": 4000,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGWC",
        "EGOV",
        "EGXE"
      ],
      "families": [
        "AMF-LOGISTICS"
      ]
    },
    {
      "code": "CARGO-15",
      "title": "Radar Component Delivery",
      "description": "Heavy radar assemblies, electronic cabinets and structural spares",
      "kg": 12000,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGPL"
      ],
      "families": [
        "AMF-LOGISTICS"
      ]
    },
    {
      "code": "CARGO-16",
      "title": "Radar Power and Cooling Pack",
      "description": "Generators, cooling assemblies and electrical distribution cabinets",
      "kg": 10500,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGPL"
      ],
      "families": [
        "AMF-LOGISTICS"
      ]
    },
    {
      "code": "CARGO-17",
      "title": "Radar Engineering Detachment",
      "description": "Six engineers, specialist tools, test equipment and deployment baggage",
      "kg": 4800,
      "people": 6,
      "kind": "mixed",
      "destinations": [
        "EGPL"
      ],
      "families": [
        "AMF-LOGISTICS"
      ]
    },
    {
      "code": "CARGO-18",
      "title": "Remote Site Sustainment",
      "description": "Engineering consumables, communications equipment, accommodation and welfare stores",
      "kg": 7000,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGPL"
      ],
      "families": [
        "AMF-LOGISTICS"
      ]
    },
    {
      "code": "CARGO-19",
      "title": "Surveillance Fleet Support",
      "description": "Aircraft spares, sensor-support equipment and diagnostic tooling",
      "kg": 4600,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGXW"
      ],
      "families": [
        "AMF-LOGISTICS"
      ]
    },
    {
      "code": "CARGO-20",
      "title": "General Station Logistics",
      "description": "Mixed palletised technical stores and maintenance supplies",
      "kg": 5000,
      "people": 0,
      "kind": "cargo",
      "destinations": [
        "EGYM",
        "EGXC",
        "EGQS",
        "EGVO",
        "EGUB",
        "EGXW",
        "EGWC",
        "EGXE",
        "EGOV",
        "EGPL"
      ],
      "families": [
        "AMF-LOGISTICS",
        "AMF-FIGHTER"
      ]
    },
    {
      "code": "PAX-20",
      "title": "20 personnel",
      "description": "100 kg per person including baggage; VISTA planning assumption.",
      "kg": 2000,
      "people": 20,
      "kind": "personnel",
      "destinations": [
        "LCRA",
        "ETAR",
        "ENGM",
        "LXGB",
        "EPRZ"
      ],
      "families": [
        "AMF-PERSONNEL"
      ]
    },
    {
      "code": "PAX-40",
      "title": "40 personnel",
      "description": "100 kg per person including baggage; VISTA planning assumption.",
      "kg": 4000,
      "people": 40,
      "kind": "personnel",
      "destinations": [
        "LCRA",
        "ETAR",
        "ENGM",
        "LXGB",
        "EPRZ"
      ],
      "families": [
        "AMF-PERSONNEL"
      ]
    },
    {
      "code": "PAX-60",
      "title": "60 personnel",
      "description": "100 kg per person including baggage; VISTA planning assumption.",
      "kg": 6000,
      "people": 60,
      "kind": "personnel",
      "destinations": [
        "LCRA",
        "ETAR",
        "ENGM",
        "LXGB",
        "EPRZ"
      ],
      "families": [
        "AMF-PERSONNEL"
      ]
    },
    {
      "code": "PAX-80",
      "title": "80 personnel",
      "description": "100 kg per person including baggage; VISTA planning assumption.",
      "kg": 8000,
      "people": 80,
      "kind": "personnel",
      "destinations": [
        "LCRA",
        "ETAR",
        "ENGM",
        "LXGB",
        "EPRZ"
      ],
      "families": [
        "AMF-PERSONNEL"
      ]
    },
    {
      "code": "DROP-PAX-20",
      "title": "20 paradrop personnel",
      "description": "100 kg per person including equipment; VISTA planning assumption.",
      "kg": 2000,
      "people": 20,
      "kind": "personnel",
      "destinations": [],
      "families": [
        "AMF-AIRDROP"
      ]
    },
    {
      "code": "DROP-PAX-40",
      "title": "40 paradrop personnel",
      "description": "100 kg per person including equipment; VISTA planning assumption.",
      "kg": 4000,
      "people": 40,
      "kind": "personnel",
      "destinations": [],
      "families": [
        "AMF-AIRDROP"
      ]
    },
    {
      "code": "DROP-PAX-60",
      "title": "60 paradrop personnel",
      "description": "100 kg per person including equipment; VISTA planning assumption.",
      "kg": 6000,
      "people": 60,
      "kind": "personnel",
      "destinations": [],
      "families": [
        "AMF-AIRDROP"
      ]
    },
    {
      "code": "DROP-CARGO-2000",
      "title": "2000 kg airdrop cargo",
      "description": "Simulated training cargo.",
      "kg": 2000,
      "people": 0,
      "kind": "cargo",
      "destinations": [],
      "families": [
        "AMF-AIRDROP"
      ]
    },
    {
      "code": "DROP-CARGO-4000",
      "title": "4000 kg airdrop cargo",
      "description": "Simulated training cargo.",
      "kg": 4000,
      "people": 0,
      "kind": "cargo",
      "destinations": [],
      "families": [
        "AMF-AIRDROP"
      ]
    },
    {
      "code": "DROP-CARGO-6000",
      "title": "6000 kg airdrop cargo",
      "description": "Simulated training cargo.",
      "kg": 6000,
      "people": 0,
      "kind": "cargo",
      "destinations": [],
      "families": [
        "AMF-AIRDROP"
      ]
    }
  ],
  "conventions": {
    "distance_unit": "nautical_miles",
    "unspecified_altitude_speed": null,
    "personnel_unit_weight_kg": 100,
    "all_new_options_enabled": true,
    "oslo_assumption": "Gardermoen ENGM",
    "wattisham_correction": "EGUW",
    "airport_coordinates": "not fabricated; airports stored separately from task waypoints"
  }
}
$roster$::jsonb;
  item jsonb; entry jsonb; field text; aircraft_code text; departure_code text; arrival_code text;
  family_row jsonb; target uuid; element_id uuid; option_id uuid; mission_id uuid; payload_id uuid;
  dep_id uuid; arr_id uuid; type_id uuid; position_no integer; table_name text;
begin
  if exists(select 1 from vista_private.catalogue_installations where code='missions-20261006-v1') then
    raise notice 'VISTA mission catalogue already installed. Existing enable switches and edits preserved.';
    return;
  end if;
  if (select count(*) from public.aircraft_types where code in ('ATLAS','TYPHOON-FGR4','TYPHOON-T3'))<>3 then
    raise exception 'Run the VISTA initial fleet setup first: Atlas and both Typhoon types are required';
  end if;
  if not exists(select 1 from public.bases where icao='EGVN' and category='primary') then
    raise exception 'Expected VISTA Brize Norton primary base is missing';
  end if;

  create table public.mission_families (
    code text primary key, title text not null,
    departure_icaos text[] not null, return_icaos text[] not null,
    enabled boolean not null default true,
    created_at timestamptz not null default now()
  );
  create table public.mission_family_aircraft_types (
    family_code text references public.mission_families(code),
    aircraft_type_id uuid references public.aircraft_types(id),
    primary key(family_code,aircraft_type_id)
  );
  create table public.mission_catalogue_options (
    id uuid primary key default gen_random_uuid(), code text not null unique,
    family_code text not null references public.mission_families(code),
    title text not null, element_id uuid references public.mission_elements(id),
    destination_base_id uuid references public.bases(id),
    enabled boolean not null default true,
    check(num_nonnulls(element_id,destination_base_id)=1)
  );
  create table public.mission_payload_presets (
    id uuid primary key default gen_random_uuid(), code text not null unique,
    title text not null, description text not null,
    kind text not null check(kind in ('cargo','personnel','mixed')),
    payload_kg integer not null check(payload_kg>0),
    personnel_count integer not null default 0 check(personnel_count>=0),
    enabled boolean not null default true,
    priority text not null default 'routine' check(priority in ('routine','exercise_support','urgent_technical'))
  );
  create table public.mission_payload_destinations (
    payload_id uuid references public.mission_payload_presets(id),
    base_id uuid references public.bases(id), primary key(payload_id,base_id)
  );
  create table public.mission_family_payloads (
    family_code text references public.mission_families(code),
    payload_id uuid references public.mission_payload_presets(id), primary key(family_code,payload_id)
  );
  alter table public.mission_elements add column catalogue_metadata jsonb not null default '{}'::jsonb check(jsonb_typeof(catalogue_metadata)='object');
  alter table public.missions add column catalogue_option_id uuid references public.mission_catalogue_options(id);
  alter table public.missions add column catalogue_metadata jsonb not null default '{}'::jsonb check(jsonb_typeof(catalogue_metadata)='object');

  for item in select value from jsonb_array_elements(roster->'bases') loop
    insert into public.bases(icao,name,category,active)
    values(item->>'icao',item->>'name',case when item->>'icao' in ('EGXC','EGQS','EGVN') then 'primary' else 'secondary' end,true)
    on conflict(icao) do nothing; -- Do not rename/reactivate an existing airfield.
  end loop;
  for item in select value from jsonb_array_elements(roster->'families') loop
    insert into public.mission_families(code,title,departure_icaos,return_icaos)
    values(item->>'code',item->>'title',array(select jsonb_array_elements_text(item->'departure')),array(select jsonb_array_elements_text(item->'arrival')));
    for aircraft_code in select jsonb_array_elements_text(item->'types') loop
      insert into public.mission_family_aircraft_types(family_code,aircraft_type_id)
      select item->>'code',id from public.aircraft_types where code=aircraft_code;
    end loop;
  end loop;
  for item in select value from jsonb_array_elements(roster->'elements') loop
    insert into public.mission_elements(code,title,description,element_type,catalogue_metadata)
    values('VISTA-'||(item->>'code'),item->>'title','User-approved virtual simulator route element. No unspecified altitude or speed is imposed.',item->>'kind',item->'metadata')
    returning id into element_id;
    position_no:=0;
    for entry in select value from jsonb_array_elements(item->'points') loop
      position_no:=position_no+1;
      insert into public.mission_element_waypoints(mission_element_id,position,identifier,latitude,longitude,altitude_ft,speed_kts,instructions)
      values(element_id,position_no,entry->>'identifier',(entry->>'latitude')::numeric,(entry->>'longitude')::numeric,
        (entry->>'altitude_ft')::integer,(entry->>'speed_kts')::integer,entry->>'instructions');
    end loop;
  end loop;
  for item in select value from jsonb_array_elements(roster->'options') loop
    element_id:=null;target:=null;
    if item->>'element' is not null then
      select id into strict element_id from public.mission_elements where code='VISTA-'||(item->>'element');
    else
      select id into strict target from public.bases where icao=item->>'destination';
    end if;
    insert into public.mission_catalogue_options(code,family_code,title,element_id,destination_base_id)
    values(item->>'code',item->>'family',item->>'title',element_id,target) returning id into option_id;
    select value into strict family_row from jsonb_array_elements(roster->'families') where value->>'code'=item->>'family';
    for aircraft_code in select jsonb_array_elements_text(family_row->'types') loop
      select id into strict type_id from public.aircraft_types where code=aircraft_code;
      for departure_code in select jsonb_array_elements_text(family_row->'departure') loop
        select id into strict dep_id from public.bases where icao=departure_code;
        for arrival_code in select value from jsonb_array_elements_text(
          case when target is null then family_row->'arrival' else jsonb_build_array(item->>'destination') end) loop
          select id into strict arr_id from public.bases where icao=arrival_code;
          insert into public.missions(code,title,description,mission_type,departure_base_id,arrival_base_id,aircraft_type_id,catalogue_option_id,catalogue_metadata)
          values('VISTA-'||(item->>'code')||'-'||departure_code||'-'||arrival_code||'-'||aircraft_code,
            (family_row->>'title')||' - '||(item->>'title')||' ['||departure_code||'/'||arrival_code||'] '||aircraft_code,
            'User-approved simulator mission. Airports are plan endpoints. Optional corridor transit and additional tasks are composed separately.',
            lower(family_row->>'code'),dep_id,arr_id,type_id,option_id,
            jsonb_build_object('route_mode',case when element_id is null then 'direct_airports' else 'task_waypoints' end,
              'family_code',item->>'family','option_code',item->>'code','element_metadata',coalesce((select catalogue_metadata from public.mission_elements where id=element_id),'{}'::jsonb)))
          returning id into mission_id;
          if element_id is not null then
            insert into public.mission_waypoints(mission_id,position,identifier,latitude,longitude,altitude_ft,speed_kts,instructions)
            select mission_id,w.position,w.identifier,w.latitude,w.longitude,w.altitude_ft,w.speed_kts,w.instructions
            from public.mission_element_waypoints w where w.mission_element_id=element_id order by w.position;
          end if;
        end loop;
      end loop;
    end loop;
  end loop;
  for item in select value from jsonb_array_elements(roster->'payloads') loop
    insert into public.mission_payload_presets(code,title,description,kind,payload_kg,personnel_count)
    values(item->>'code',item->>'title',item->>'description',item->>'kind',(item->>'kg')::integer,(item->>'people')::integer)
    returning id into payload_id;
    for field in select jsonb_array_elements_text(item->'destinations') loop
      insert into public.mission_payload_destinations(payload_id,base_id) select payload_id,id from public.bases where icao=field;
    end loop;
    for field in select jsonb_array_elements_text(item->'families') loop
      insert into public.mission_family_payloads(family_code,payload_id) values(field,payload_id);
    end loop;
  end loop;
  foreach table_name in array array['mission_families','mission_family_aircraft_types','mission_catalogue_options','mission_payload_presets','mission_payload_destinations','mission_family_payloads'] loop
    execute format('alter table public.%I enable row level security',table_name);
    execute format('create policy catalogue_read on public.%I for select to authenticated using (exists(select 1 from public.pilots where auth_user_id=(select auth.uid()) and status=''active''))',table_name);
    execute format('revoke all on public.%I from public,anon,authenticated',table_name);
    execute format('grant select on public.%I to authenticated',table_name);
    execute format('grant all on public.%I to service_role',table_name);
  end loop;
  insert into vista_private.catalogue_installations(code) values('missions-20261006-v1');
end $setup$;

-- Restricted management boundary for the app's forthcoming option switches.
create or replace function public.set_mission_catalogue_enabled(p_kind text,p_code text,p_enabled boolean)
returns void language plpgsql security definer set search_path='' as $toggle$
begin
  if not vista_private.can_operate() then raise exception 'Active operations/admin access required'; end if;
  if p_enabled is null then raise exception 'Provide an enabled state'; end if;
  if p_kind='family' then update public.mission_families set enabled=p_enabled where code=p_code;
  elsif p_kind='option' then update public.mission_catalogue_options set enabled=p_enabled where code=p_code;
  elsif p_kind='payload' then update public.mission_payload_presets set enabled=p_enabled where code=p_code;
  elsif p_kind='element' then update public.mission_elements set active=p_enabled where code=p_code;
  else raise exception 'Unknown catalogue kind'; end if;
  if not found then raise exception 'Catalogue item not found'; end if;
  update public.missions m set active=f.enabled and o.enabled and coalesce(e.active,true)
  from public.mission_catalogue_options o join public.mission_families f on f.code=o.family_code
  left join public.mission_elements e on e.id=o.element_id
  where m.catalogue_option_id=o.id and m.active is distinct from (f.enabled and o.enabled and coalesce(e.active,true));
end $toggle$;
revoke all on function public.set_mission_catalogue_enabled(text,text,boolean) from public,anon,authenticated;
grant execute on function public.set_mission_catalogue_enabled(text,text,boolean) to authenticated;
commit;

-- Verification output. These queries do not change state.
select f.code,f.title,f.enabled,count(o.id) as option_count from public.mission_families f
left join public.mission_catalogue_options o on o.family_code=f.code group by f.code order by f.code;
select count(*) as total_seeded_missions from public.missions where catalogue_option_id is not null;
select kind,count(*) as preset_count from public.mission_payload_presets group by kind;
