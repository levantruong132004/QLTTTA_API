class Payment {
  final int paymentId;
  final int invoiceId;
  final int amount;
  final DateTime paymentDate;
  final String paymentMethod;
  final int accountantId;
  final String? accountantName;

  Payment({
    required this.paymentId,
    required this.invoiceId,
    required this.amount,
    required this.paymentDate,
    required this.paymentMethod,
    required this.accountantId,
    this.accountantName,
  });

  factory Payment.fromJson(Map<String, dynamic> json) {
    return Payment(
      paymentId: json['paymentId'] ?? json['PaymentId'] ?? 0,
      invoiceId: json['invoiceId'] ?? json['InvoiceId'] ?? 0,
      amount: json['amount'] ?? json['Amount'] ?? 0,
      paymentDate: json['paymentDate'] != null
          ? DateTime.parse(json['paymentDate'])
          : (json['PaymentDate'] != null
              ? DateTime.parse(json['PaymentDate'])
              : DateTime.now()),
      paymentMethod: json['paymentMethod'] ?? json['PaymentMethod'] ?? '',
      accountantId: json['accountantId'] ?? json['AccountantId'] ?? 0,
      accountantName: json['accountantName'] ?? json['AccountantName'],
    );
  }
}

class PendingPayment {
  final int invoiceId;
  final String? studentName;
  final String? courseName;
  final int amount;
  final DateTime requestDate;

  PendingPayment({
    required this.invoiceId,
    this.studentName,
    this.courseName,
    required this.amount,
    required this.requestDate,
  });

  factory PendingPayment.fromJson(Map<String, dynamic> json) {
    return PendingPayment(
      invoiceId: json['invoiceId'] ?? json['InvoiceId'] ?? 0,
      studentName: json['studentName'] ?? json['StudentName'],
      courseName: json['courseName'] ?? json['CourseName'],
      amount: json['amount'] ?? json['Amount'] ?? 0,
      requestDate: json['requestDate'] != null
          ? DateTime.parse(json['requestDate'])
          : (json['RequestDate'] != null
              ? DateTime.parse(json['RequestDate'])
              : DateTime.now()),
    );
  }
}
