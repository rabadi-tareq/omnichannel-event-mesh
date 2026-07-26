import { Injectable, inject, signal, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import * as signalR from '@microsoft/signalr';

export interface OrderStatusUpdate {
  orderId: string;
  status: string;
  timestamp: string;
}

@Injectable({
  providedIn: 'root'
})
export class SignalRService {
  private hubConnection!: signalR.HubConnection;
  private platformId = inject(PLATFORM_ID);

  // Angular Signal to hold real-time order updates
  public latestUpdate = signal<OrderStatusUpdate | null>(null);
  public connectionState = signal<string>('Disconnected');

  // Per-order status map keyed by orderId
  public orderStatuses = signal<Record<string, OrderStatusUpdate>>({});

  public startConnection(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/order')
      .withAutomaticReconnect()
      .build();

    this.connectionState.set('Connecting');

    this.hubConnection
      .start()
      .then(() => {
        this.connectionState.set('Connected');
        console.log('SignalR Connection Established');
      })
      .catch(err => {
        this.connectionState.set('Error');
        console.error('Error starting SignalR connection:', err);
      });

    this.registerOrderStateListener();
  }

  private registerOrderStateListener(): void {
    this.hubConnection.on('ReceiveOrderUpdate', (update: OrderStatusUpdate) => {
      this.latestUpdate.set(update);
      this.orderStatuses.update(current => ({ ...current, [update.orderId]: update }));
    });
  }
}
