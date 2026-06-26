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

    public MemberRegistrationController(
        CustomerService customerService,
        ICustomerRepository customerRepository,
        IMemberAccountRepository memberRepository,
        IAuthService authService)
    {
        _customerService = customerService ?? throw new ArgumentNullException(nameof(customerService));
        _customerRepository = customerRepository ?? throw new ArgumentNullException(nameof(customerRepository));
        _memberRepository = memberRepository ?? throw new ArgumentNullException(nameof(memberRepository));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
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
        if (string.IsNullOrWhiteSpace(request.Username)
            || string.IsNullOrWhiteSpace(request.Password)
            || string.IsNullOrWhiteSpace(request.PhoneNumber)
            || string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(new { error = "Username, password, phone number, and full name are required." });
        }

        if (request.Password.Length < 8)
            return BadRequest(new { error = "Password must be at least 8 characters." });

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var normalizedPhone = Customer.NormalizePhoneNumber(request.PhoneNumber);

        if (string.IsNullOrEmpty(normalizedPhone))
            return BadRequest(new { error = "Invalid phone number." });

        if (await _memberRepository.UsernameExistsAsync(normalizedUsername, cancellationToken))
            return Conflict(new { error = "Username is already taken." });

        try
        {
            // Find or create customer profile
            var customer = await _customerRepository.GetByPhoneNumberAsync(normalizedPhone, cancellationToken);
            if (customer == null)
            {
                customer = await _customerService.CreateAsync(
                    normalizedPhone,
                    request.FullName.Trim(),
                    request.Email?.Trim(),
                    cancellationToken);
            }
            else
            {
                // Update profile details if customer already exists (e.g. placeholder from transfer)
                customer.FullName = request.FullName.Trim();
                if (!string.IsNullOrWhiteSpace(request.Email))
                    customer.Email = request.Email.Trim();
                _customerRepository.Update(customer);
                await _customerRepository.SaveChangesAsync(cancellationToken);
            }

            // Activate existing placeholder or create new member account
            var existingMember = await _memberRepository.GetByCustomerIdAsync(customer.Id, cancellationToken);
            if (existingMember != null)
            {
                if (!string.IsNullOrEmpty(existingMember.PasswordHash))
                    return Conflict(new { error = "A member account is already registered for this phone number." });

                existingMember.Username = normalizedUsername;
                existingMember.PasswordHash = _authService.HashPassword(request.Password);
                existingMember.FullName = request.FullName.Trim();
                existingMember.Status = MemberAccountStatus.Active;
                _memberRepository.Update(existingMember);
                await _memberRepository.SaveChangesAsync(cancellationToken);

                var activationAuthResult = await _authService.LoginMemberAsync(normalizedUsername, request.Password, cancellationToken);
                if (!activationAuthResult.Success || activationAuthResult.Member == null)
                    return StatusCode(500, new { error = "Activation succeeded but login failed." });

                var activationResponse = new MemberRegistrationResponse(
                    existingMember.Id,
                    customer.Id,
                    existingMember.Username,
                    existingMember.FullName,
                    activationAuthResult.Token!,
                    activationAuthResult.ExpiresAt!.Value);

                return Ok(activationResponse);
            }

            // Create linked member account
            var member = new MemberAccount
            {
                Username = normalizedUsername,
                PasswordHash = _authService.HashPassword(request.Password),
                FullName = request.FullName.Trim(),
                CustomerId = customer.Id,
                Status = MemberAccountStatus.Active
            };

            await _memberRepository.AddAsync(member, cancellationToken);
            await _memberRepository.SaveChangesAsync(cancellationToken);

            // Auto-login after registration
            var authResult = await _authService.LoginMemberAsync(normalizedUsername, request.Password, cancellationToken);
            if (!authResult.Success || authResult.Member == null)
                return StatusCode(500, new { error = "Registration succeeded but login failed." });

            var response = new MemberRegistrationResponse(
                member.Id,
                customer.Id,
                member.Username,
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
    string Username,
    string Password,
    string FullName,
    string PhoneNumber,
    string? Email);

public record MemberRegistrationResponse(
    Guid MemberAccountId,
    Guid CustomerId,
    string Username,
    string FullName,
    string Token,
    DateTime ExpiresAt);
