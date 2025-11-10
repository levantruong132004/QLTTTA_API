class Invoice {
  final int invoiceId;
  final int registrationId;
  final String? studentName;
  final String? courseName;
  final String? className;
  final int amount;
  final DateTime issueDate;
  final DateTime dueDate;
  final String status; // 'unpaid', 'pending_confirmation', 'paid'
  final DateTime? paidDate;
  final String? signatureBase64;

  Invoice({
    required this.invoiceId,
    required this.registrationId,
    this.studentName,
    this.courseName,
    this.className,
    required this.amount,
    required this.issueDate,
    required this.dueDate,
    required this.status,
    this.paidDate,
    this.signatureBase64,
  });

  factory Invoice.fromJson(Map<String, dynamic> json) {
    return Invoice(
      invoiceId: json['invoiceId'] ?? json['InvoiceId'] ?? 0,
      registrationId: json['registrationId'] ?? json['RegistrationId'] ?? 0,
      studentName: json['studentName'] ?? json['StudentName'],
      courseName: json['courseName'] ?? json['CourseName'],
      className: json['className'] ?? json['ClassName'],
      amount: json['amount'] ?? json['Amount'] ?? 0,
      issueDate: json['issueDate'] != null
          ? DateTime.parse(json['issueDate'])
          : (json['IssueDate'] != null
              ? DateTime.parse(json['IssueDate'])
              : DateTime.now()),
      dueDate: json['dueDate'] != null
          ? DateTime.parse(json['dueDate'])
          : (json['DueDate'] != null
              ? DateTime.parse(json['DueDate'])
              : DateTime.now()),
      status: json['status'] ?? json['Status'] ?? 'unpaid',
      paidDate: json['paidDate'] != null
          ? DateTime.parse(json['paidDate'])
          : (json['PaidDate'] != null
              ? DateTime.parse(json['PaidDate'])
              : null),
      signatureBase64: json['signatureBase64'] ?? json['SignatureBase64'],
    );
  }

  String getStatusText() {
    switch (status.toLowerCase()) {
      case 'paid':
      case 'đã thanh toán':
        return 'Đã thanh toán';
      case 'pending_confirmation':
      case 'chờ xác nhận':
        return 'Chờ xác nhận';
      case 'unpaid':
      case 'chưa thanh toán':
        return 'Chưa thanh toán';
      default:
        return status;
    }
  }

  bool get isOverdue {
    if (status.toLowerCase() == 'paid') return false;
    return DateTime.now().isAfter(dueDate);
  }
}
