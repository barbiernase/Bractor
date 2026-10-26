"""
Die Nutzlast-Tabelle — eine Domänen-Nachricht reist als Typname + Bytes ihrer DTO-Message (Cqrs.Leitung/cqrs.proto, `Nutzlast`). Die Tabelle
ist das Generat (nutzlast_tabelle.py): nichts wird aus Klassen-Namen abgeleitet; packen und auspacken sind Umkehrungen, über den echten Draht.
"""

from dataclasses import dataclass

import betterproto
import pytest

from cqrs_client.leitung import Nutzlast
from cqrs_client.mapper import NutzlastTabelle, PayloadMapper


@dataclass
class FakeDto(betterproto.Message):
    ref: str = betterproto.string_field(1)


def test_packen_und_auspacken_ueber_den_draht():
    mapper = PayloadMapper({"Fake": FakeDto})
    n = Nutzlast().parse(bytes(mapper.packe(FakeDto(ref="hin"))))
    assert n.typ == "Fake"
    assert mapper.entpacke(n) == FakeDto(ref="hin")


def test_unbekannter_typ_ist_nicht_verpackbar():
    @dataclass
    class UnbekanntDto(betterproto.Message):
        x: int = betterproto.int32_field(1)

    with pytest.raises(TypeError):
        PayloadMapper({"Fake": FakeDto}).packe(UnbekanntDto(x=1))


def test_typ_kommt_nur_aus_der_tabelle():
    tabelle = NutzlastTabelle({"AnderesWort": FakeDto})   # der Name der Klasse spielt keine Rolle
    assert tabelle.typ(FakeDto) == "AnderesWort"
    assert tabelle.packe(FakeDto(ref="x")).typ == "AnderesWort"


def test_klasse_ausserhalb_der_tabelle_ist_nicht_verpackbar():
    with pytest.raises(TypeError):
        NutzlastTabelle({}).packe(FakeDto(ref="x"))


def test_eine_klasse_unter_zwei_typen_ist_ein_fehler():
    with pytest.raises(ValueError):
        NutzlastTabelle({"A": FakeDto, "B": FakeDto})
