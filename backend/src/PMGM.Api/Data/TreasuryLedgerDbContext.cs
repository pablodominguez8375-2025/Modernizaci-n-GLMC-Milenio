using Microsoft.EntityFrameworkCore;
using PMGM.Api.Modules.Treasury.Entities;

namespace PMGM.Api.Data;

public sealed class TreasuryLedgerDbContext(DbContextOptions<TreasuryLedgerDbContext> options) : DbContext(options)
{
    public DbSet<MemberCharge> MemberCharges => Set<MemberCharge>();
    public DbSet<MemberPayment> MemberPayments => Set<MemberPayment>();
    public DbSet<MemberPaymentAllocation> MemberPaymentAllocations => Set<MemberPaymentAllocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("core");

        modelBuilder.Entity<MemberCharge>(entity =>
        {
            entity.ToTable("member_charges");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Concept).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Period).HasMaxLength(40);
            entity.Property(x => x.ChargeType).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(40).IsRequired();
            entity.Property(x => x.SourceReference).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.CreatedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.MemberId, x.DueDate });
            entity.HasIndex(x => new { x.MemberId, x.Status });
            entity.HasIndex(x => new { x.MemberId, x.ChargeType, x.IssuedDate });
        });

        modelBuilder.Entity<MemberPayment>(entity =>
        {
            entity.ToTable("member_payments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.Method).HasMaxLength(40).IsRequired();
            entity.Property(x => x.ReceiptNumber).HasMaxLength(120);
            entity.Property(x => x.SourceReference).HasMaxLength(500);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.RecordedBySubject).HasMaxLength(320).IsRequired();
            entity.Property(x => x.RecordedAtUtc).IsRequired();
            entity.HasIndex(x => new { x.OrganizationId, x.MemberId, x.PaymentDate });
            entity.HasIndex(x => x.ReceiptDocumentId);
        });

        modelBuilder.Entity<MemberPaymentAllocation>(entity =>
        {
            entity.ToTable("member_payment_allocations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.CreatedAtUtc).IsRequired();
            entity.HasIndex(x => x.PaymentId);
            entity.HasIndex(x => x.ChargeId);
            entity.HasIndex(x => new { x.PaymentId, x.ChargeId }).IsUnique();
        });
    }
}
