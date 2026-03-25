using CardService.Application.DTOs.Responses;
using MediatR;

namespace CardService.Application.Commands.Cards;

public sealed record CreateCardCommand(
    Guid UserId,
    string CardholderName,
    int ExpMonth,
    int ExpYear,
    string CardNumber,
    decimal CreditLimit,
    decimal OutstandingBalance,
    int BillingCycleStartDay,
    bool IsDefault) : IRequest<CardResult>;
