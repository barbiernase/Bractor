using System;
using Abstractions;

namespace Domain.Projections;

public record GetLagerbestand(Guid ArtikelId) : IQuery;
public record GetAllLagerbestaende() : IQuery;