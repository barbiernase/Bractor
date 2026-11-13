// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// RECHNERDIENST — ein Prozess je Rechner (docs/10 E18, docs/04 §4.7, docs/06 §6.5): `--umgebung <name> --rechner <name>` (bzw. CQRS_UMGEBUNG,
// CQRS_RECHNER). Er nimmt aus der Umgebung die App-Instanzen dieses Rechners, lässt ihre Quellen laufen (Läufer, Annahme auf dem Port der Instanz)
// und gibt über die Leitung hinein wie eine App — mit dem Token der Instanz, bis zur Quittung.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

return await Client.Infrastructure.Quellen.Rechnerdienst.LaufeAsync(args, Domain.Laufzeit.GeneratedAnwendung.Instanz);
