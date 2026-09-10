using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NonCash.API.DTOs;
using NonCash.Core.Entities;
using NonCash.Core.Interfaces;
using NonCash.Core.Services;

namespace NonCash.API.Controllers;

[ApiController]
[Route("api/v1/members")]
[AllowAnonymous]
public class MemberRegistrationController : ControllerBase
{
    private readonly CustomerService _customerService;
    private readonly ICustomerRepository _customerRepository;
    private readonly IMemberAccountRepository _memberRepository;
    private readonly IAuthService _authService;
    private readonly IVoucherTransferService _voucherTransferService;

    public MemberRegistrationController(
        CustomerService customerService,
        ICustomerRepository customerRepository,
        IMemberAccountRepository memberRepository,
        IAuthService authService,
        IVoucherTransferService voucherTransferService)
    {
        _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _voucherTransferService = voucherTransferService ?? throw new ArgumentNullException(nameof(voucherTransferService));
    }

    /// <summary>
    /// Self-registration for individual members/customers.
    /// Creates a Customer profile (or links to an existing one) and a linked MemberAccount.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<MemberRegistrationResponse>> Register(
        MemberRegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.PhoneNumber)
            || string.IsNullOrWhiteSpace(request.FullName)
            || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error = "Password, phone number, full name, and email are required." });
        }

        // Email is mandatory: it is the primary voucher notification channel.
        var email = request.Email.Trim();
        if (!System.Net.Mail.MailAddress.TryCreate(email, out _))
            return BadRequest(new { error = "Invalid email address." });

        if (request.Password.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        var normalizedPhone = Customer.NormalizePhoneNumber(request.PhoneNumber);

        if (normalizedPhone.Length is < 9 or > 15)
            return BadRequest(new { error = "Invalid phone number. Enter the number you will sign in with, for example 0913660575." });

        try
        {
            // Find or create customer profile
            var customer = await _customerRepository.GetByPhoneNumberAsync(normalizedPhone, cancellationToken);

            // Matrix row 11 (gap #7): blacklisted customers cannot self-register a member
            // account. Generic message — no blacklist detail leak.
            if (customer?.Status == CustomerStatus.Blacklisted)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "This account cannot be registered." });

            if (customer == null)
            {
                customer = await _customerService.CreateAsync(
                    normalizedPhone,
                    request.FullName.Trim(),
                    email,
                    cancellationToken);
            }
            else
            {
                // Update profile details if customer already exists (e.g. placeholder from transfer).
                // Email is unique per customer: reject if another customer already owns it.
                var normalizedEmail = Customer.NormalizeEmail(email)!;
                if (await _customerRepository.EmailExistsAsync(normalizedEmail, excludeCustomerId: customer.Id, cancellationToken))
                    return Conflict(new { error = $"A customer with email '{normalizedEmail}' already exists." });

                customer.FullName = request.FullName.Trim();
                customer.Email = normalizedEmail;
                _customerRepository.Update(customer);
                await _customerRepository.SaveChangesAsync(cancellationToken);
            }

            // Activate existing placeholder or create new member account
            var existingMember = await _memberRepository.GetByCustomerIdAsync(customer.Id, cancellationToken);
            if (existingMember != null)
            {
                if (!string.IsNullOrEmpty(existingMember.PasswordHash))
                    return Conflict(new { error = "A member account is already registered for this phone number." });

                existingMember.PasswordHash = _authService.HashPassword(request.Password);
                existingMember.FullName = request.FullName.Trim();
                existingMember.Status = MemberAccountStatus.Active;
                _memberRepository.Update(existingMember);
                await _memberRepository.SaveChangesAsync(cancellationToken);

                var activationAuthResult = await _authService.LoginMemberAsync(normalizedPhone, request.Password, cancellationToken);
                if (!activationAuthResult.Success || activationAuthResult.Member == null)
                    return StatusCode(500, new { error = "Activation succeeded but login failed." });

                // CR-2026-09-10-30 row 3: gifts held for this phone while it was a placeholder
                // are emailed with their accept link now that the account has an email address.
                await _voucherTransferService.NotifyPendingGiftsAsync(existingMember.Id, cancellationToken);

                var activationResponse = new MemberRegistrationResponse(
                    existingMember.Id,
                    customer.Id,
                    existingMember.FullName,
                    activationAuthResult.Token!,
                    activationAuthResult.ExpiresAt!.Value);

                return Ok(activationResponse);
            }

            // Create linked member account
            var member = new MemberAccount
            {
                PasswordHash = _authService.HashPassword(request.Password),
                FullName = request.FullName.Trim(),
                CustomerId = customer.Id,
                Status = MemberAccountStatus.Active
            };

            await _memberRepository.AddAsync(member, cancellationToken);
            await _memberRepository.SaveChangesAsync(cancellationToken);

            // Auto-login after registration
            var authResult = await _authService.LoginMemberAsync(normalizedPhone, request.Password, cancellationToken);
            if (!authResult.Success || authResult.Member == null)
                return StatusCode(500, new { error = "Registration succeeded but login failed." });

            var response = new MemberRegistrationResponse(
                member.Id,
                customer.Id,
                member.FullName,
                authResult.Token!,
                authResult.ExpiresAt!.Value);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public record MemberRegisterRequest(
    string Password,
    string FullName,
    string PhoneNumber,
    string Email);

public record MemberRegistrationResponse(
    Guid MemberAccountId,
    Guid CustomerId,
    string FullName,
    string Token,
    DateTime ExpiresAt);
