import { Component, Input } from '@angular/core';

@Component({
  selector: 'shared-button',
  standalone: true,
  template: '<button>{{label}}</button>'
})
export class ButtonComponent {
  @Input() label: string = '';
  @Input() variant: 'primary' | 'secondary' = 'primary';
}
