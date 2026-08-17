# REPO-PFAD: Domain.Client.Worker.Python.ML/domain_client/domain_registry.py
"""
CategoryRegistry mit den Domain-Typen dieses Systems.
Entspricht MessageTypeMapping.cs auf C#-Seite.
"""

from cqrs_client.registry import CategoryRegistry

from domain_client.generated import (
    BildVerfuegbarDto,
    ImagePairKomplettDto,
    ImagePairErstelltDto,
    BildPaarDurchKiKlassifiziertDto,
    PaarNichtKomplettDto,
    KlassifiziereBildPaarDurchKiDto,
    # ── Trainingslauf (Konzept §4) ──
    TrainingAngefordertDto,
    TrainingAbgebrochenDto,
    MeldeTrainingBegonnenDto,
    MeldeFortschrittDto,
    MeldeTrainingAbgeschlossenDto,
    MeldeTrainingGescheitertDto,
)

EVENT_TYPES: frozenset[type] = frozenset({
    BildVerfuegbarDto,
    ImagePairKomplettDto,
    ImagePairErstelltDto,
    BildPaarDurchKiKlassifiziertDto,
    PaarNichtKomplettDto,
    # Der TrainingWorker abonniert diese:
    TrainingAngefordertDto,
    TrainingAbgebrochenDto,
})

COMMAND_TYPES: frozenset[type] = frozenset({
    KlassifiziereBildPaarDurchKiDto,
    # Der TrainingWorker meldet Fortschritt über diese:
    MeldeTrainingBegonnenDto,
    MeldeFortschrittDto,
    MeldeTrainingAbgeschlossenDto,
    MeldeTrainingGescheitertDto,
})

TRIGGER_TYPES: frozenset[type] = frozenset()
QUERY_TYPES: frozenset[type] = frozenset()
QUERY_RESPONSE_TYPES: frozenset[type] = frozenset()


def create_registry() -> CategoryRegistry:
    return CategoryRegistry(
        events=EVENT_TYPES,
        commands=COMMAND_TYPES,
        triggers=TRIGGER_TYPES,
        queries=QUERY_TYPES,
        query_responses=QUERY_RESPONSE_TYPES,
    )
