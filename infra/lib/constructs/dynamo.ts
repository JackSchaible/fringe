import * as cdk from 'aws-cdk-lib';
import { AttributeType, Billing, TableV2 } from 'aws-cdk-lib/aws-dynamodb';
import { Construct } from 'constructs';

export class FringeDynamo extends Construct {
  public readonly table: TableV2;

  constructor(scope: Construct, id: string) {
    super(scope, id);

    this.table = new TableV2(this, 'Table', {
      tableName: 'fringe',
      partitionKey: { name: 'pk', type: AttributeType.STRING },
      sortKey: { name: 'sk', type: AttributeType.STRING },
      billing: Billing.onDemand(),
      globalSecondaryIndexes: [
        {
          indexName: 'entity-type-index',
          partitionKey: { name: 'entityType', type: AttributeType.STRING },
          sortKey: { name: 'pk', type: AttributeType.STRING },
        },
      ],
      removalPolicy: cdk.RemovalPolicy.RETAIN,
    });
  }
}
