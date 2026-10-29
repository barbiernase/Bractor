// Die typisierten Ids der Aggregate, die dieser Client zeigt und adressiert (docs/10 §10.4): kurze Namen für
// Abstractions.Id<…> — der Client-Zustand (Auswahl, Markierung, Sammel-Ziel) trägt Ids, die sagen, worauf sie zeigen.
global using ImagePairId = Abstractions.Id<Domain.ImagePair.ImagePair>;
global using DatensatzId = Abstractions.Id<Domain.Datensatz.Datensatz>;
global using ModellId = Abstractions.Id<Domain.Modell.Modell>;
global using TrainingslaufId = Abstractions.Id<Domain.Trainingslauf.Trainingslauf>;
