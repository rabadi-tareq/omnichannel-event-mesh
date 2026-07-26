import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
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

  // Signal array to track event log history
  public orderEvents = signal<OrderStatusUpdate[]>([]);

  ngOnInit(): void {
    this.signalRService.startConnection();
  }
}
