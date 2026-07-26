import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { SignalRService, OrderStatusUpdate } from '../../signalr.service';

@Component({
  selector: 'app-order-dashboard',
  standalone: true,
  imports: [CommonModule, DatePipe],
  templateUrl: './order-dashboard.html',
  styleUrl: './order-dashboard.css'
})
export class OrderDashboardComponent implements OnInit {
  public signalRService = inject(SignalRService);
  private http = inject(HttpClient);

  // Signal array to track event log history
  public orderEvents = signal<OrderStatusUpdate[]>([]);

  public pickupPending = signal<Record<string, boolean>>({});

  ngOnInit(): void {
    this.signalRService.startConnection();
  }

  confirmPickup(orderId: string): void {
    this.pickupPending.update(s => ({ ...s, [orderId]: true }));
    this.http.post(`/api/orders/${orderId}/pickup`, { associateId: 'associate-001' }).subscribe({
      error: () => this.pickupPending.update(s => ({ ...s, [orderId]: false }))
    });
  }
}
