kias-shutdown = KIAS shutting down.
kias-online = KIAS online.
kias-enable = Enable KIAS
kias-disable = Shut down KIAS
kias-device-status = KIAS: {$status}
kias-status-offline = OFFLINE
kias-status-online = OK
kias-status-nopower = NO POWER
kias-status-nodatapath = NO DATA PATH
kias-status-disconnected = DISCONNECTED
kias-status-duplicatecore = FAULT: MULTIPLE CORES
kias-sender = KIAS
kias-window-title = KIAS — Ship automation
kias-refresh = Refresh
kias-port-announce = Announce
kias-port-announce-description = Announce the configured message through KIAS.
kias-custom-message = Custom speaker message
kias-save = Save
kias-mode-link = Link
kias-mode-diagnose = Diagnose
kias-mode-coverage = Show coverage
kias-mode-test = TEST
kias-source-selected = Source selected. Use the tool on a KIAS speaker.
kias-test-cooldown = TEST is cooling down (10 seconds).
kias-test-start = KIAS self test started.
kias-test-done = KIAS self test complete. Previous state restored.
kias-coverage-legend = DATA coverage: X = device, + = connected, . = disconnected
kias-port-motion = Presence
kias-port-motion-description = Fires when a tracked entity enters scanner coverage.
kias-registration-locked = Registration is locked or the server is offline.
kias-transponder-registered = Crew transponder registered.
kias-lock-registration = Lock registration
kias-unlock-registration = Unlock registration
kias-biometric-alive = Alive
kias-biometric-critical = Critical
kias-biometric-dead = Dead
kias-radiation = {$name}: {$value} rad/s
kias-anomaly-growth = Attention. Anomaly growth detected: {$location}.
kias-atmos-danger = Attention. Dangerous atmosphere: {$location}.
kias-sector-center = center
kias-sector-fore-port = fore port
kias-sector-fore-starboard = fore starboard
kias-sector-aft-port = aft port
kias-sector-aft-starboard = aft starboard
kias-contact = Bluespace disturbance. {$disposition} vessel. Range {$range} km, bearing {$bearing}°.
kias-contact-friendly = Friendly
kias-contact-neutral = Neutral
kias-contact-unknown = Unknown
kias-contact-hostile = Hostile
kias-autopilot-arrived = Autopilot: destination reached.
kias-owner-only = Only the ship owner can operate the master key.
kias-hull-other-sectors = other sectors
kias-pdc-manual-priority = Weapon reserved by KIAS point defence. Manual firing is temporarily locked.
kias-page-overview = Overview
kias-page-atmos = Atmosphere
kias-page-crew = Crew
kias-page-power = Power
kias-page-defence = Defence
kias-page-navigation = Navigation
kias-page-faults = Faults
kias-alert-normal = Normal
kias-alert-contact = Contact
kias-alert-battle = Battle alert
kias-alert-emergency = Emergency
kias-no-faults = No device faults.
kias-pdc-reserved = { $weapon }: reserved by KIAS point defence; manual firing locked.
kias-pdc-enabled = Automatic point defence enabled.
kias-pdc-disabled = Automatic point defence disabled.
kias-protocols-title = Ship protocols (owner configuration)
kias-protocol-trigger = Trigger
kias-protocol-action = Action
kias-protocol-target = Target device
kias-protocol-group = Light group
kias-protocol-port = Input port
kias-protocol-cooldown = Cooldown in seconds (1–600)
kias-protocol-enabled = Protocol enabled
kias-protocol-value = Enable / close selected target
kias-protocol-new = New protocol
kias-protocol-whole-ship = Whole ship / no individual target
kias-remove = Remove
kias-action-announce = Announce
kias-action-record = Record
kias-action-lights = Set light group
kias-action-suppression = Use suppression cartridge
kias-action-relay = Set relay
kias-action-pdc = Set point defence
kias-action-deviceport = Activate device input
kias-action-mayday = Send MAYDAY
kias-power-unavailable = Power server unavailable.
kias-power-statistics = { $channel }: supply { $supply } kW / demand { $demand } kW.
kias-power-deficit = Power deficit: { $channel }.
kias-config-owner = Only the ship owner can configure protocols on an online display.
kias-crew-dead = Death detected: { $location }.
kias-crew-critical = Critical crew condition: { $location }.
kias-fire-alarm = Fire alarm: { $location }.
kias-mayday = MAYDAY. { $ship }. { $reason }
kias-trigger-hullimpact = Hull impact
kias-trigger-hulldamage = Structural damage
kias-trigger-collision = Ship collision
kias-trigger-atmosdanger = Atmosphere or fire alarm
kias-trigger-anomalygrowth = Anomaly growth
kias-trigger-contact = Bluespace contact
kias-trigger-arrival = Autopilot arrival
kias-trigger-weaponflash = Ship weapon fire
kias-trigger-proximity = Proximity contact
kias-trigger-crewcritical = Critical crew condition
kias-trigger-crewdead = Crew death
kias-trigger-vesselcritical = Critical vessel condition
kias-trigger-powerdeficit = Power deficit
kias-trigger-manual = Manual trigger
kias-weapon-flash = Nearby ship weapon fire detected.
kias-proximity-contact = Proximity contact: { $range } m, { $disposition }.
kias-pdc-enable = Enable automatic point defence
kias-pdc-disable = Disable automatic point defence
kias-relay-close = Close selected channel
kias-relay-open = Open selected channel
kias-relay-highvoltage = Channel: HV
kias-relay-mediumvoltage = Channel: MV
kias-relay-apc = Channel: LV
kias-relay-data = Channel: DATA
kias-mode-group = Configure light group
kias-light-group-set = Light group: { $group }.
kias-lights-on = Enable light group
kias-lights-off = Disable light group
kias-suppress = Discharge suppression cartridge
kias-suppression-activated = Fire suppression activated: { $location }.
kias-port-suppress = Suppress fire
kias-port-suppress-description = Discharges the installed suppression cartridge.
kias-grid-collision = Grid collision. Relative speed: { $speed } m/s.
kias-hull-impact = Hull impact: { $location }. Hits: { $amount }.
kias-hull-destroyed = Hull structures destroyed: { $location }. Count: { $amount }.
kias-hull-damage = Structural damage: { $location }. Damage: { $amount }.

kias-trigger-fire = Fire detected
kias-run-manual = Run manual protocols
kias-reset-alert = Reset alert
kias-mode-room = Set room label
kias-room-set = Room: { $room }

kias-coverage-colors = DATA: teal — connected coverage; cyan — cable; yellow frame — selected device. Snapshot, 9×9 tiles.

kias-protocol-any-contact = Any contact classification
kias-protocol-minimum = Minimum event value (damage, speed, range or deficit)
kias-protocol-crew-unavailable = Require distress AND no responsive registered crew


ent-KiasAtmosServerBoard = KIAS atmosphere server circuit board

ent-KiasCollisionMonitorBoard = KIAS collision monitor circuit board

ent-KiasCoreBoard = KIAS core circuit board

ent-KiasCrewServerBoard = KIAS crew server circuit board

ent-KiasDefenceServerBoard = KIAS defence server circuit board

ent-KiasDisplayBoard = KIAS display circuit board

ent-KiasHorizonBoard = KIAS Horizon bluespace interferometer circuit board

ent-KiasHullSensorBoard = KIAS hull impact sensor circuit board

ent-KiasIntegrityMonitorBoard = KIAS hull integrity monitor circuit board

ent-KiasLightControllerBoard = KIAS light group controller circuit board

ent-KiasNavigationServerBoard = KIAS navigation server circuit board

ent-KiasPdcRadarBoard = KIAS point defence radar circuit board

ent-KiasPowerServerBoard = KIAS power server circuit board

ent-KiasProximitySensorBoard = KIAS proximity sensor circuit board

ent-KiasRecorderBoard = KIAS recorder circuit board

ent-KiasRelayBoard = KIAS four channel relay circuit board

ent-KiasRoomScannerBoard = KIAS modular room scanner circuit board

ent-KiasSpeakerBoard = KIAS speaker circuit board

ent-KiasSuppressionBoard = KIAS fire suppression module circuit board

ent-KiasWeaponFlashDetectorBoard = KIAS weapon flash detector circuit board
